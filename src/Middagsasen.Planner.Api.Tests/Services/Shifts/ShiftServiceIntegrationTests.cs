using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services;
using Middagsasen.Planner.Api.Services.Events;
using Middagsasen.Planner.Api.Services.Resources;
using Middagsasen.Planner.Api.Services.Shifts;
using Middagsasen.Planner.Api.Services.SmsSender;
using Middagsasen.Planner.Api.Tests.Infrastructure;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Middagsasen.Planner.Api.Tests.Services.Shifts
{
    /// <summary>
    /// Integrasjonstester mot <see cref="IShiftService"/> med ekte repository, ekte <see cref="TrainerNotifier"/>
    /// og falsk <see cref="ISmsSender"/>.
    /// </summary>
    [Collection("Database")]
    public class ShiftServiceIntegrationTests
    {
        // Oppgavene i testene er 15.01.2026 09:00–15:00 norsk tid (UTC+1).
        private static readonly DateTime ResourceStart = new(2026, 1, 15, 9, 0, 0);
        private static readonly DateTime ResourceEnd = new(2026, 1, 15, 15, 0, 0);
        private static readonly DateTimeOffset BeforeResource = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        // 14:30 UTC = 15:30 norsk tid: avsluttet i norsk tid, men ikke hvis tidene feilaktig ble tolket som UTC.
        private static readonly DateTimeOffset AfterResourceInNorway = new(2026, 1, 15, 14, 30, 0, TimeSpan.Zero);

        private readonly DatabaseFixture _fixture;
        private readonly ISmsSender _smsSender;

        public ShiftServiceIntegrationTests(DatabaseFixture fixture)
        {
            _fixture = fixture;
            _smsSender = Substitute.For<ISmsSender>();
            _smsSender.SendMessages(Arg.Any<IEnumerable<SmsMessage>>()).Returns(new SmsResult { Success = true });
        }

        private IShiftService CreateService(PlannerDbContext context, int userId, bool isAdmin = false, DateTimeOffset? now = null)
        {
            var currentUser = Substitute.For<ICurrentUserService>();
            currentUser.UserId.Returns(userId);
            currentUser.IsAdmin.Returns(isAdmin);
            var notifier = new TrainerNotifier(new TrainerRepository(context), _smsSender, NullLogger<TrainerNotifier>.Instance);
            var clock = new FakeTimeProvider(now ?? BeforeResource);
            return new ShiftService(new ShiftRepository(context), new ResourceReader(context, clock), currentUser, notifier, clock);
        }

        private static string UniqueName(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

        /// <summary>Lagrede brukernavn er normaliserte telefonnumre, og trenerne varsles på SMS til nummeret.</summary>
        private static string UniquePhoneNo() => Random.Shared.Next(40000000, 99999999).ToString();

        private static async Task<User> SeedUser(PlannerDbContext context, string firstName = "Test", bool isAdmin = false)
        {
            var user = new User
            {
                UserName = UniquePhoneNo(),
                FirstName = firstName,
                LastName = "Bruker",
                Created = DateTime.UtcNow,
                IsAdmin = isAdmin,
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();
            return user;
        }

        private static async Task<EventResource> SeedResource(PlannerDbContext context, int shiftCount = 2, params int[] trainerUserIds)
        {
            var resourceType = new ResourceType { Name = UniqueName("Heis"), DefaultShiftCount = shiftCount };
            foreach (var trainerId in trainerUserIds)
                resourceType.Trainers.Add(new ResourceTypeTrainer { UserId = trainerId });
            context.ResourceTypes.Add(resourceType);

            var evt = new Event
            {
                Name = UniqueName("Event"),
                StartTime = ResourceStart,
                EndTime = ResourceEnd,
                Resources =
                [
                    new EventResource
                    {
                        ResourceType = resourceType,
                        StartTime = ResourceStart,
                        EndTime = ResourceEnd,
                        ShiftCount = shiftCount,
                    },
                ],
            };
            context.Events.Add(evt);
            await context.SaveChangesAsync();
            return evt.Resources.Single();
        }

        private static async Task<EventResourceUser> SeedShift(PlannerDbContext context, EventResource resource, int userId, string comment = "Original")
        {
            var shift = new EventResourceUser
            {
                EventResourceId = resource.EventResourceId,
                UserId = userId,
                StartTime = ResourceStart,
                EndTime = ResourceEnd,
                Comment = comment,
            };
            context.Shifts.Add(shift);
            await context.SaveChangesAsync();
            return shift;
        }

        private static async Task SeedTraining(PlannerDbContext context, int userId, int resourceTypeId, bool trainingComplete)
        {
            context.ResourceTypeTrainings.Add(new ResourceTypeTraining { UserId = userId, ResourceTypeId = resourceTypeId, TrainingComplete = trainingComplete });
            await context.SaveChangesAsync();
        }

        private async Task<List<EventResourceUser>> GetShifts(int resourceId)
        {
            using var verify = _fixture.CreateContext();
            return await verify.Shifts.AsNoTracking().Where(s => s.EventResourceId == resourceId).ToListAsync();
        }

        private async Task<ResourceTypeTraining?> GetTraining(int userId, int resourceTypeId)
        {
            using var verify = _fixture.CreateContext();
            return await verify.ResourceTypeTrainings.AsNoTracking().SingleOrDefaultAsync(t => t.UserId == userId && t.ResourceTypeId == resourceTypeId);
        }

        #region SignUp

        [Fact]
        public async Task SignUp_Self_PersistsShiftWithResourceTimes_AndReturnsResourceWithFlags()
        {
            using var seed = _fixture.CreateContext();
            var user = await SeedUser(seed);
            var resource = await SeedResource(seed, shiftCount: 2);

            using var context = _fixture.CreateContext();
            var result = await CreateService(context, user.UserId).SignUp(resource.EventResourceId, new SignUpRequest { Comment = "Hei" });

            var shift = Assert.Single(await GetShifts(resource.EventResourceId));
            Assert.Equal(user.UserId, shift.UserId);
            Assert.Equal(ResourceStart, shift.StartTime);
            Assert.Equal(ResourceEnd, shift.EndTime);
            Assert.Equal("Hei", shift.Comment);

            Assert.Equal(resource.EventResourceId, result.Resource.Id);
            Assert.False(result.Resource.CanSignUp); // allerede påmeldt
            Assert.True(result.Resource.IsMissingStaff);
            Assert.False(result.Resource.IsFull);
            Assert.False(result.Resource.MustAnswerTraining);
            var responseShift = Assert.Single(result.Resource.Shifts);
            Assert.True(responseShift.IsMine);
            Assert.True(responseShift.CanEdit);
            Assert.True(responseShift.CanWithdraw);
            Assert.Null(result.ChangedTraining);
            Assert.Empty(result.Warnings);
            await _smsSender.DidNotReceiveWithAnyArgs().SendMessages(default!);
        }

        [Fact]
        public async Task SignUp_ThrowsDomainValidation_WhenTrainingAnswerIsMissing()
        {
            using var seed = _fixture.CreateContext();
            var trainer = await SeedUser(seed, "Trener");
            var user = await SeedUser(seed);
            var resource = await SeedResource(seed, 2, trainer.UserId);

            using var context = _fixture.CreateContext();
            var ex = await Assert.ThrowsAsync<DomainValidationException>(
                () => CreateService(context, user.UserId).SignUp(resource.EventResourceId, new SignUpRequest()));

            Assert.Contains("opplæring", ex.Message);
            Assert.Empty(await GetShifts(resource.EventResourceId));
            Assert.Null(await GetTraining(user.UserId, resource.ResourceTypeId));
        }

        [Fact]
        public async Task SignUp_TrainingNotCompleted_CreatesTrainingRequest_AndSendsSmsAfterCommit()
        {
            using var seed = _fixture.CreateContext();
            var trainer = await SeedUser(seed, "Trener");
            var user = await SeedUser(seed, "Ola");
            var resource = await SeedResource(seed, 2, trainer.UserId);

            // Sjekker fra en annen tilkobling at påmeldingen er committet når SMS-en sendes.
            var committedWhenSmsSent = false;
            IEnumerable<SmsMessage>? sent = null;
            _smsSender.SendMessages(Arg.Any<IEnumerable<SmsMessage>>()).Returns(ci =>
            {
                sent = ci.Arg<IEnumerable<SmsMessage>>().ToList();
                using var other = _fixture.CreateContext();
                committedWhenSmsSent = other.Shifts.Any(s => s.EventResourceId == resource.EventResourceId && s.UserId == user.UserId);
                return new SmsResult { Success = true };
            });

            using var context = _fixture.CreateContext();
            var result = await CreateService(context, user.UserId).SignUp(resource.EventResourceId, new SignUpRequest { TrainingCompleted = false });

            Assert.True(committedWhenSmsSent);
            var message = Assert.Single(sent!);
            Assert.Contains("Ola Bruker ønsker opplæring på", message.Body);
            Assert.Contains("15.01.2026", message.Body);

            var training = await GetTraining(user.UserId, resource.ResourceTypeId);
            Assert.NotNull(training);
            Assert.False(training.TrainingComplete);
            Assert.Null(training.ConfirmedBy);

            Assert.NotNull(result.ChangedTraining);
            Assert.Equal(training.ResourceTypeTrainingId, result.ChangedTraining.Id);
            Assert.Equal(user.UserId, result.ChangedTraining.UserId);
            Assert.Equal(resource.ResourceTypeId, result.ChangedTraining.ResourceTypeId);
            Assert.False(result.ChangedTraining.TrainingComplete);
            Assert.False(result.Resource.MustAnswerTraining);
            Assert.True(Assert.Single(result.Resource.Shifts).NeedsTraining);
            Assert.Empty(result.Warnings);
        }

        [Fact]
        public async Task SignUp_TrainingCompleted_CreatesCompletedTraining_WithoutSms()
        {
            using var seed = _fixture.CreateContext();
            var trainer = await SeedUser(seed, "Trener");
            var user = await SeedUser(seed);
            var resource = await SeedResource(seed, 2, trainer.UserId);

            using var context = _fixture.CreateContext();
            var result = await CreateService(context, user.UserId).SignUp(resource.EventResourceId, new SignUpRequest { TrainingCompleted = true });

            var training = await GetTraining(user.UserId, resource.ResourceTypeId);
            Assert.NotNull(training);
            Assert.True(training.TrainingComplete);
            Assert.Equal(user.UserId, training.ConfirmedBy);
            Assert.NotNull(training.Confirmed);
            Assert.True(result.ChangedTraining!.TrainingComplete);
            Assert.False(Assert.Single(result.Resource.Shifts).NeedsTraining);
            await _smsSender.DidNotReceiveWithAnyArgs().SendMessages(default!);
        }

        [Theory]
        [InlineData(true, null)]
        [InlineData(false, null)]
        [InlineData(true, true)]
        [InlineData(false, false)]
        public async Task SignUp_UserHasTraining_AnswerIsNullOrSame_ChangesNothing(bool stored, bool? answer)
        {
            using var seed = _fixture.CreateContext();
            var trainer = await SeedUser(seed, "Trener");
            var user = await SeedUser(seed);
            var resource = await SeedResource(seed, 2, trainer.UserId);
            await SeedTraining(seed, user.UserId, resource.ResourceTypeId, trainingComplete: stored);

            using var context = _fixture.CreateContext();
            var result = await CreateService(context, user.UserId).SignUp(resource.EventResourceId, new SignUpRequest { TrainingCompleted = answer });

            var training = await GetTraining(user.UserId, resource.ResourceTypeId);
            Assert.Equal(stored, training!.TrainingComplete);
            Assert.Null(training.Confirmed); // ikke bekreftet på nytt
            Assert.Null(training.ConfirmedBy);
            Assert.Null(result.ChangedTraining);
            Assert.Single(await GetShifts(resource.EventResourceId));
            await _smsSender.DidNotReceiveWithAnyArgs().SendMessages(default!);
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public async Task SignUp_UserHasTraining_DifferentAnswer_UpdatesTraining_AndSendsSmsOnlyWhenRequested(bool stored, bool answer)
        {
            using var seed = _fixture.CreateContext();
            var trainer = await SeedUser(seed, "Trener");
            var user = await SeedUser(seed);
            var resource = await SeedResource(seed, 2, trainer.UserId);
            await SeedTraining(seed, user.UserId, resource.ResourceTypeId, trainingComplete: stored);

            using var context = _fixture.CreateContext();
            var result = await CreateService(context, user.UserId).SignUp(resource.EventResourceId, new SignUpRequest { TrainingCompleted = answer });

            using var verify = _fixture.CreateContext();
            var training = Assert.Single(await verify.ResourceTypeTrainings.AsNoTracking()
                .Where(t => t.UserId == user.UserId && t.ResourceTypeId == resource.ResourceTypeId).ToListAsync());
            Assert.Equal(answer, training.TrainingComplete);
            Assert.Equal(answer ? user.UserId : null, training.ConfirmedBy);
            Assert.Equal(answer, training.Confirmed.HasValue);
            Assert.Equal(training.ResourceTypeTrainingId, result.ChangedTraining!.Id);
            Assert.Equal(answer, result.ChangedTraining.TrainingComplete);
            Assert.Equal(!answer, Assert.Single(result.Resource.Shifts).NeedsTraining);
            await _smsSender.ReceivedWithAnyArgs(answer ? 0 : 1).SendMessages(default!);
        }

        [Fact]
        public async Task SignUp_Admin_OtherUserWithTrainingRequest_ConfirmsTrainingAsAdmin()
        {
            using var seed = _fixture.CreateContext();
            var trainer = await SeedUser(seed, "Trener");
            var user = await SeedUser(seed);
            var admin = await SeedUser(seed, "Admin", isAdmin: true);
            var resource = await SeedResource(seed, 2, trainer.UserId);
            await SeedTraining(seed, user.UserId, resource.ResourceTypeId, trainingComplete: false);

            using var context = _fixture.CreateContext();
            var result = await CreateService(context, admin.UserId, isAdmin: true)
                .SignUp(resource.EventResourceId, new SignUpRequest { UserId = user.UserId, TrainingCompleted = true });

            var training = await GetTraining(user.UserId, resource.ResourceTypeId);
            Assert.True(training!.TrainingComplete);
            Assert.Equal(admin.UserId, training.ConfirmedBy);
            Assert.Equal(user.UserId, result.ChangedTraining!.UserId);
            Assert.Equal(admin.UserId, result.ChangedTraining.ConfirmedById);
            await _smsSender.DidNotReceiveWithAnyArgs().SendMessages(default!);
        }

        [Fact]
        public async Task SignUp_Trainer_ThrowsForbidden_ForOtherUser_AndSavesNothing()
        {
            using var seed = _fixture.CreateContext();
            var trainer = await SeedUser(seed, "Trener");
            var user = await SeedUser(seed);
            var resource = await SeedResource(seed, 2, trainer.UserId);

            using var context = _fixture.CreateContext();
            await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateService(context, trainer.UserId)
                .SignUp(resource.EventResourceId, new SignUpRequest { UserId = user.UserId, TrainingCompleted = true }));

            Assert.Empty(await GetShifts(resource.EventResourceId));
            Assert.Null(await GetTraining(user.UserId, resource.ResourceTypeId));
        }

        [Fact]
        public async Task SignUp_IgnoresTrainingAnswer_WhenResourceTypeHasNoTraining()
        {
            using var seed = _fixture.CreateContext();
            var user = await SeedUser(seed);
            var resource = await SeedResource(seed);

            using var context = _fixture.CreateContext();
            var result = await CreateService(context, user.UserId).SignUp(resource.EventResourceId, new SignUpRequest { TrainingCompleted = false });

            Assert.Single(await GetShifts(resource.EventResourceId));
            Assert.Null(await GetTraining(user.UserId, resource.ResourceTypeId));
            Assert.Null(result.ChangedTraining);
            await _smsSender.DidNotReceiveWithAnyArgs().SendMessages(default!);
        }

        [Fact]
        public async Task SignUp_KeepsSignUp_AndReturnsWarning_WhenSmsFails()
        {
            using var seed = _fixture.CreateContext();
            var trainer = await SeedUser(seed, "Trener");
            var user = await SeedUser(seed);
            var resource = await SeedResource(seed, 2, trainer.UserId);
            _smsSender.SendMessages(Arg.Any<IEnumerable<SmsMessage>>()).Returns(new SmsResult { Success = false, Info = "500" });

            using var context = _fixture.CreateContext();
            var result = await CreateService(context, user.UserId).SignUp(resource.EventResourceId, new SignUpRequest { TrainingCompleted = false });

            Assert.Equal([ShiftService.SmsFailedWarning], result.Warnings);
            Assert.Single(await GetShifts(resource.EventResourceId));
            Assert.False((await GetTraining(user.UserId, resource.ResourceTypeId))!.TrainingComplete);
        }

        [Fact]
        public async Task SignUp_KeepsSignUp_AndReturnsWarning_WhenSmsSenderThrows()
        {
            using var seed = _fixture.CreateContext();
            var trainer = await SeedUser(seed, "Trener");
            var user = await SeedUser(seed);
            var resource = await SeedResource(seed, 2, trainer.UserId);
            _smsSender.SendMessages(Arg.Any<IEnumerable<SmsMessage>>()).ThrowsAsync(new HttpRequestException("nede"));

            using var context = _fixture.CreateContext();
            var result = await CreateService(context, user.UserId).SignUp(resource.EventResourceId, new SignUpRequest { TrainingCompleted = false });

            Assert.Equal([ShiftService.SmsFailedWarning], result.Warnings);
            Assert.Single(await GetShifts(resource.EventResourceId));
        }

        [Fact]
        public async Task SignUp_ThrowsDomainValidation_OnUniqueIndex_AndSendsNoSms_WhenTransactionFails()
        {
            using var seed = _fixture.CreateContext();
            var trainer = await SeedUser(seed, "Trener");
            var user = await SeedUser(seed);
            var resource = await SeedResource(seed, 5, trainer.UserId);

            // Simulerer en samtidig påmelding av samme bruker som slipper forbi duplikatsjekken: raden settes inn i
            // samme transaksjon rett før lagringen, så det er den unike indeksen som stopper påmeldingen.
            PlannerDbContext context = null!;
            var interceptor = new BeforeSaveInterceptor(() => context.Database.ExecuteSqlInterpolatedAsync(
                $"insert into EventResourceUsers (EventResourceId, UserId) values ({resource.EventResourceId}, {user.UserId})"));
            context = _fixture.CreateContext(interceptor);
            using var _ = context;

            var ex = await Assert.ThrowsAsync<DomainValidationException>(
                () => CreateService(context, user.UserId).SignUp(resource.EventResourceId, new SignUpRequest { TrainingCompleted = false }));

            Assert.Equal(ShiftService.DuplicateMessage, ex.Message);
            Assert.Empty(await GetShifts(resource.EventResourceId));
            Assert.Null(await GetTraining(user.UserId, resource.ResourceTypeId));
            await _smsSender.DidNotReceiveWithAnyArgs().SendMessages(default!);
        }

        [Fact]
        public async Task UniqueIndex_PreventsSameUserTwiceOnSameResource()
        {
            using var seed = _fixture.CreateContext();
            var user = await SeedUser(seed);
            var resource = await SeedResource(seed);
            await SeedShift(seed, resource, user.UserId);

            using var context = _fixture.CreateContext();
            context.Shifts.Add(new EventResourceUser { EventResourceId = resource.EventResourceId, UserId = user.UserId });

            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            Assert.Contains(ShiftRepository.UniqueShiftIndexName, ex.InnerException!.Message);
        }

        [Fact]
        public async Task SignUp_RejectsUser_WhenResourceIsFull_ButAdminCanOverbook()
        {
            using var seed = _fixture.CreateContext();
            var first = await SeedUser(seed);
            var user = await SeedUser(seed);
            var admin = await SeedUser(seed, "Admin", isAdmin: true);
            var resource = await SeedResource(seed, shiftCount: 1);
            await SeedShift(seed, resource, first.UserId);

            using (var context = _fixture.CreateContext())
            {
                var ex = await Assert.ThrowsAsync<DomainValidationException>(
                    () => CreateService(context, user.UserId).SignUp(resource.EventResourceId, new SignUpRequest()));
                Assert.Equal(ShiftService.FullMessage, ex.Message);
            }

            using (var context = _fixture.CreateContext())
            {
                var result = await CreateService(context, admin.UserId, isAdmin: true)
                    .SignUp(resource.EventResourceId, new SignUpRequest { UserId = user.UserId });
                Assert.Equal(2, result.Resource.Shifts.Count());
                Assert.True(result.Resource.IsFull);
                Assert.True(result.Resource.CanSignUp); // admin kan fortsatt overbooke seg selv
            }
        }

        [Fact]
        public async Task SignUp_Concurrent_OnlyOneGetsTheLastSpot()
        {
            using var seed = _fixture.CreateContext();
            var a = await SeedUser(seed);
            var b = await SeedUser(seed);
            var resource = await SeedResource(seed, shiftCount: 1);

            using var contextA = _fixture.CreateContext();
            using var contextB = _fixture.CreateContext();
            var results = await Task.WhenAll(
                Attempt(() => CreateService(contextA, a.UserId).SignUp(resource.EventResourceId, new SignUpRequest())),
                Attempt(() => CreateService(contextB, b.UserId).SignUp(resource.EventResourceId, new SignUpRequest())));

            Assert.Single(results, r => r is null);
            Assert.Single(results, r => r is DomainValidationException);
            Assert.Single(await GetShifts(resource.EventResourceId));
        }

        /// <summary>Kjører <paramref name="call"/> og returnerer unntaket den kastet, eller <c>null</c> hvis den lyktes.</summary>
        private static async Task<Exception?> Attempt(Func<Task> call)
        {
            try
            {
                await call();
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task SignUp_RejectsDuplicate_AlsoForAdmin(bool isAdmin)
        {
            using var seed = _fixture.CreateContext();
            var user = await SeedUser(seed, isAdmin: isAdmin);
            var resource = await SeedResource(seed, shiftCount: 5);
            await SeedShift(seed, resource, user.UserId);

            using var context = _fixture.CreateContext();
            var ex = await Assert.ThrowsAsync<DomainValidationException>(
                () => CreateService(context, user.UserId, isAdmin).SignUp(resource.EventResourceId, new SignUpRequest()));

            Assert.Equal(ShiftService.DuplicateMessage, ex.Message);
            Assert.Single(await GetShifts(resource.EventResourceId));
        }

        [Fact]
        public async Task SignUp_RejectsUser_WhenResourceHasEndedInNorwegianTime_ButAdminCan()
        {
            using var seed = _fixture.CreateContext();
            var user = await SeedUser(seed);
            var admin = await SeedUser(seed, "Admin", isAdmin: true);
            var resource = await SeedResource(seed);

            using (var context = _fixture.CreateContext())
            {
                var ex = await Assert.ThrowsAsync<DomainValidationException>(
                    () => CreateService(context, user.UserId, now: AfterResourceInNorway).SignUp(resource.EventResourceId, new SignUpRequest()));
                Assert.Equal(ShiftService.PastMessage, ex.Message);
            }

            using (var context = _fixture.CreateContext())
            {
                var result = await CreateService(context, admin.UserId, isAdmin: true, now: AfterResourceInNorway)
                    .SignUp(resource.EventResourceId, new SignUpRequest { UserId = user.UserId });
                Assert.True(result.Resource.IsPast);
                var shift = Assert.Single(result.Resource.Shifts);
                Assert.True(shift.CanEdit);
                Assert.True(shift.CanWithdraw);
            }
        }

        [Fact]
        public async Task SignUp_ThrowsForbidden_WhenNonAdminSignsUpOtherUser()
        {
            using var seed = _fixture.CreateContext();
            var user = await SeedUser(seed);
            var other = await SeedUser(seed);
            var resource = await SeedResource(seed);

            using var context = _fixture.CreateContext();
            await Assert.ThrowsAsync<ForbiddenAccessException>(
                () => CreateService(context, user.UserId).SignUp(resource.EventResourceId, new SignUpRequest { UserId = other.UserId }));
            Assert.Empty(await GetShifts(resource.EventResourceId));
        }

        [Fact]
        public async Task SignUp_Admin_SignsUpOtherUser_WithTrainingAnswer()
        {
            using var seed = _fixture.CreateContext();
            var trainer = await SeedUser(seed, "Trener");
            var user = await SeedUser(seed);
            var admin = await SeedUser(seed, "Admin", isAdmin: true);
            var resource = await SeedResource(seed, 2, trainer.UserId);

            using var context = _fixture.CreateContext();
            var result = await CreateService(context, admin.UserId, isAdmin: true)
                .SignUp(resource.EventResourceId, new SignUpRequest { UserId = user.UserId, TrainingCompleted = true });

            var training = await GetTraining(user.UserId, resource.ResourceTypeId);
            Assert.True(training!.TrainingComplete);
            Assert.Equal(admin.UserId, training.ConfirmedBy);
            var shift = Assert.Single(result.Resource.Shifts);
            Assert.Equal(user.UserId, shift.User.Id);
            Assert.False(shift.IsMine);
            Assert.True(shift.CanEdit);
            // Admin har selv ikke svart på opplæring for vakttypen.
            Assert.True(result.Resource.MustAnswerTraining);
        }

        [Fact]
        public async Task SignUp_ThrowsDomainValidation_WhenTimesAreOutsideResource()
        {
            using var seed = _fixture.CreateContext();
            var user = await SeedUser(seed);
            var resource = await SeedResource(seed);

            using var context = _fixture.CreateContext();
            var ex = await Assert.ThrowsAsync<DomainValidationException>(() => CreateService(context, user.UserId)
                .SignUp(resource.EventResourceId, new SignUpRequest { StartTime = ResourceStart.AddHours(-1) }));

            Assert.Equal(ShiftService.InvalidTimesMessage, ex.Message);
        }

        [Fact]
        public async Task SignUp_ThrowsEntityNotFound_WhenResourceDoesNotExist()
        {
            using var seed = _fixture.CreateContext();
            var user = await SeedUser(seed);

            using var context = _fixture.CreateContext();
            await Assert.ThrowsAsync<EntityNotFoundException>(() => CreateService(context, user.UserId).SignUp(999999, new SignUpRequest()));
        }


        /// <summary>Simulerer at en samtidig forespørsel (egen tilkobling, committet) oppretter opplæringsraden rett før lagringen.</summary>
        private BeforeSaveInterceptor ConcurrentTrainingInserter(int userId, int resourceTypeId) => new(async () =>
        {
            using var other = _fixture.CreateContext();
            other.ResourceTypeTrainings.Add(new ResourceTypeTraining { UserId = userId, ResourceTypeId = resourceTypeId, TrainingComplete = true });
            await other.SaveChangesAsync();
        });

        private static async Task<EventResource> SeedResourceOfSameType(PlannerDbContext context, EventResource existing)
        {
            var resource = new EventResource
            {
                EventId = existing.EventId,
                ResourceTypeId = existing.ResourceTypeId,
                StartTime = ResourceStart,
                EndTime = ResourceEnd,
                ShiftCount = existing.ShiftCount,
            };
            context.EventResource.Add(resource);
            await context.SaveChangesAsync();
            return resource;
        }

        [Fact]
        public async Task SignUp_RetriesOnce_WhenTrainingIsCreatedConcurrently_AndUpdatesItWhenAnswerDiffers()
        {
            using var seed = _fixture.CreateContext();
            var trainer = await SeedUser(seed, "Trener");
            var user = await SeedUser(seed);
            var resource = await SeedResource(seed, 2, trainer.UserId);

            using var context = _fixture.CreateContext(ConcurrentTrainingInserter(user.UserId, resource.ResourceTypeId));
            var result = await CreateService(context, user.UserId).SignUp(resource.EventResourceId, new SignUpRequest { TrainingCompleted = false });

            Assert.Equal(user.UserId, Assert.Single(await GetShifts(resource.EventResourceId)).UserId);
            // Andre forsøk ser raden fra den samtidige forespørselen (fullført) og oppdaterer den, siden svaret er ulikt.
            using var verify = _fixture.CreateContext();
            var training = Assert.Single(await verify.ResourceTypeTrainings.AsNoTracking()
                .Where(t => t.UserId == user.UserId && t.ResourceTypeId == resource.ResourceTypeId).ToListAsync());
            Assert.False(training.TrainingComplete);
            Assert.Equal(training.ResourceTypeTrainingId, result.ChangedTraining!.Id);
            Assert.Single(result.Resource.Shifts);
            await _smsSender.ReceivedWithAnyArgs(1).SendMessages(default!);
        }

        [Fact]
        public async Task SignUp_Concurrent_OnTwoResourcesOfSameType_BothSucceed_WithOneTrainingRow()
        {
            using var seed = _fixture.CreateContext();
            var trainer = await SeedUser(seed, "Trener");
            var user = await SeedUser(seed);
            var first = await SeedResource(seed, 2, trainer.UserId);
            var second = await SeedResourceOfSameType(seed, first);

            using var contextA = _fixture.CreateContext();
            using var contextB = _fixture.CreateContext();
            await Task.WhenAll(
                CreateService(contextA, user.UserId).SignUp(first.EventResourceId, new SignUpRequest { TrainingCompleted = false }),
                CreateService(contextB, user.UserId).SignUp(second.EventResourceId, new SignUpRequest { TrainingCompleted = false }));

            Assert.Single(await GetShifts(first.EventResourceId));
            Assert.Single(await GetShifts(second.EventResourceId));
            using var verify = _fixture.CreateContext();
            Assert.Equal(1, await verify.ResourceTypeTrainings.CountAsync(t => t.UserId == user.UserId && t.ResourceTypeId == first.ResourceTypeId));
            // Bare forespørselen som opprettet raden varsler trenerne.
            await _smsSender.ReceivedWithAnyArgs(1).SendMessages(default!);
        }

        #endregion

        #region Change

        [Fact]
        public async Task Change_Owner_UpdatesTimesAndComment()
        {
            using var seed = _fixture.CreateContext();
            var owner = await SeedUser(seed);
            var resource = await SeedResource(seed);
            var shift = await SeedShift(seed, resource, owner.UserId);

            using var context = _fixture.CreateContext();
            var result = await CreateService(context, owner.UserId).Change(shift.EventResourceUserId, new ChangeShiftRequest
            {
                StartTime = ResourceStart.AddHours(1),
                Comment = "Ny",
            });

            var dbShift = Assert.Single(await GetShifts(resource.EventResourceId));
            Assert.Equal(ResourceStart.AddHours(1), dbShift.StartTime);
            Assert.Equal(ResourceEnd, dbShift.EndTime); // null = behold
            Assert.Equal("Ny", dbShift.Comment);
            Assert.Equal(owner.UserId, dbShift.UserId);
            Assert.Equal("Ny", Assert.Single(result.Resource.Shifts).Comment);
        }

        [Fact]
        public async Task Change_Owner_NullCommentClearsComment()
        {
            using var seed = _fixture.CreateContext();
            var owner = await SeedUser(seed);
            var resource = await SeedResource(seed);
            var shift = await SeedShift(seed, resource, owner.UserId);

            using var context = _fixture.CreateContext();
            await CreateService(context, owner.UserId).Change(shift.EventResourceUserId, new ChangeShiftRequest());

            Assert.Null(Assert.Single(await GetShifts(resource.EventResourceId)).Comment);
        }

        [Fact]
        public async Task Change_Owner_ThrowsForbidden_WhenMovingShiftToOtherUser()
        {
            using var seed = _fixture.CreateContext();
            var owner = await SeedUser(seed);
            var other = await SeedUser(seed);
            var resource = await SeedResource(seed);
            var shift = await SeedShift(seed, resource, owner.UserId);

            using var context = _fixture.CreateContext();
            await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateService(context, owner.UserId)
                .Change(shift.EventResourceUserId, new ChangeShiftRequest { UserId = other.UserId, Comment = "Flyttet" }));

            var dbShift = Assert.Single(await GetShifts(resource.EventResourceId));
            Assert.Equal(owner.UserId, dbShift.UserId);
            Assert.Equal("Original", dbShift.Comment);
        }

        [Fact]
        public async Task Change_Owner_ThrowsDomainValidation_WhenResourceHasEnded()
        {
            using var seed = _fixture.CreateContext();
            var owner = await SeedUser(seed);
            var resource = await SeedResource(seed);
            var shift = await SeedShift(seed, resource, owner.UserId);

            using var context = _fixture.CreateContext();
            await Assert.ThrowsAsync<DomainValidationException>(() => CreateService(context, owner.UserId, now: AfterResourceInNorway)
                .Change(shift.EventResourceUserId, new ChangeShiftRequest { Comment = "For sent" }));
        }

        [Fact]
        public async Task Change_Trainer_ThrowsForbidden_AlsoWithTrainingAnswer()
        {
            using var seed = _fixture.CreateContext();
            var owner = await SeedUser(seed);
            var trainer = await SeedUser(seed, "Trener");
            var resource = await SeedResource(seed, 2, trainer.UserId);
            await SeedTraining(seed, owner.UserId, resource.ResourceTypeId, trainingComplete: false);
            var shift = await SeedShift(seed, resource, owner.UserId);

            using var context = _fixture.CreateContext();
            await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateService(context, trainer.UserId)
                .Change(shift.EventResourceUserId, new ChangeShiftRequest { Comment = "Trener", TrainingCompleted = true }));
            Assert.Equal("Original", Assert.Single(await GetShifts(resource.EventResourceId)).Comment);
            Assert.False((await GetTraining(owner.UserId, resource.ResourceTypeId))!.TrainingComplete);
        }

        [Fact]
        public async Task Change_Admin_MovesShiftToOtherUser_EvenAfterResourceHasEnded()
        {
            using var seed = _fixture.CreateContext();
            var owner = await SeedUser(seed);
            var other = await SeedUser(seed);
            var admin = await SeedUser(seed, "Admin", isAdmin: true);
            var resource = await SeedResource(seed);
            var shift = await SeedShift(seed, resource, owner.UserId);

            using var context = _fixture.CreateContext();
            var result = await CreateService(context, admin.UserId, isAdmin: true, now: AfterResourceInNorway)
                .Change(shift.EventResourceUserId, new ChangeShiftRequest { UserId = other.UserId, Comment = "Flyttet" });

            var dbShift = Assert.Single(await GetShifts(resource.EventResourceId));
            Assert.Equal(other.UserId, dbShift.UserId);
            Assert.Equal("Flyttet", dbShift.Comment);
            Assert.Equal(other.UserId, Assert.Single(result.Resource.Shifts).User.Id);
        }

        [Fact]
        public async Task Change_Admin_ThrowsDomainValidation_WhenMovingToUserAlreadyOnResource()
        {
            using var seed = _fixture.CreateContext();
            var owner = await SeedUser(seed);
            var other = await SeedUser(seed);
            var admin = await SeedUser(seed, "Admin", isAdmin: true);
            var resource = await SeedResource(seed, shiftCount: 5);
            var shift = await SeedShift(seed, resource, owner.UserId);
            await SeedShift(seed, resource, other.UserId);

            using var context = _fixture.CreateContext();
            var ex = await Assert.ThrowsAsync<DomainValidationException>(() => CreateService(context, admin.UserId, isAdmin: true)
                .Change(shift.EventResourceUserId, new ChangeShiftRequest { UserId = other.UserId }));
            Assert.Equal(ShiftService.DuplicateMessage, ex.Message);
        }

        [Fact]
        public async Task Change_ThrowsEntityNotFound_WhenShiftDoesNotExist()
        {
            using var seed = _fixture.CreateContext();
            var user = await SeedUser(seed);

            using var context = _fixture.CreateContext();
            await Assert.ThrowsAsync<EntityNotFoundException>(() => CreateService(context, user.UserId).Change(999999, new ChangeShiftRequest()));
        }


        [Fact]
        public async Task Change_Admin_MovesShiftToUserWithoutTraining_RequiresTrainingAnswer()
        {
            using var seed = _fixture.CreateContext();
            var trainer = await SeedUser(seed, "Trener");
            var owner = await SeedUser(seed);
            var other = await SeedUser(seed);
            var admin = await SeedUser(seed, "Admin", isAdmin: true);
            var resource = await SeedResource(seed, 2, trainer.UserId);
            var shift = await SeedShift(seed, resource, owner.UserId);

            using var context = _fixture.CreateContext();
            var ex = await Assert.ThrowsAsync<DomainValidationException>(() => CreateService(context, admin.UserId, isAdmin: true)
                .Change(shift.EventResourceUserId, new ChangeShiftRequest { UserId = other.UserId }));

            Assert.Equal(ShiftService.TrainingAnswerRequiredMessage(resource.ResourceType.Name), ex.Message);
            Assert.Equal(owner.UserId, Assert.Single(await GetShifts(resource.EventResourceId)).UserId);
            Assert.Null(await GetTraining(other.UserId, resource.ResourceTypeId));
        }

        [Fact]
        public async Task Change_Admin_MovesShift_TrainingNotCompleted_CreatesTrainingRequest_AndSendsSmsAfterCommit()
        {
            using var seed = _fixture.CreateContext();
            var trainer = await SeedUser(seed, "Trener");
            var owner = await SeedUser(seed);
            var other = await SeedUser(seed, "Kari");
            var admin = await SeedUser(seed, "Admin", isAdmin: true);
            var resource = await SeedResource(seed, 2, trainer.UserId);
            var shift = await SeedShift(seed, resource, owner.UserId);

            var committedWhenSmsSent = false;
            IEnumerable<SmsMessage>? sent = null;
            _smsSender.SendMessages(Arg.Any<IEnumerable<SmsMessage>>()).Returns(ci =>
            {
                sent = ci.Arg<IEnumerable<SmsMessage>>().ToList();
                using var verify = _fixture.CreateContext();
                committedWhenSmsSent = verify.Shifts.Any(s => s.EventResourceUserId == shift.EventResourceUserId && s.UserId == other.UserId);
                return new SmsResult { Success = true };
            });

            using var context = _fixture.CreateContext();
            var result = await CreateService(context, admin.UserId, isAdmin: true)
                .Change(shift.EventResourceUserId, new ChangeShiftRequest { UserId = other.UserId, TrainingCompleted = false });

            Assert.True(committedWhenSmsSent);
            Assert.Contains("Kari Bruker ønsker opplæring på", Assert.Single(sent!).Body);
            var training = await GetTraining(other.UserId, resource.ResourceTypeId);
            Assert.False(training!.TrainingComplete);
            Assert.Null(training.ConfirmedBy);
            Assert.Equal(training.ResourceTypeTrainingId, result.ChangedTraining!.Id);
            Assert.Equal(other.UserId, result.ChangedTraining.UserId);
            Assert.True(Assert.Single(result.Resource.Shifts).NeedsTraining);
            Assert.Empty(result.Warnings);
        }

        [Fact]
        public async Task Change_Admin_MovesShift_TrainingCompleted_CreatesCompletedTrainingConfirmedByAdmin_WithoutSms()
        {
            using var seed = _fixture.CreateContext();
            var trainer = await SeedUser(seed, "Trener");
            var owner = await SeedUser(seed);
            var other = await SeedUser(seed);
            var admin = await SeedUser(seed, "Admin", isAdmin: true);
            var resource = await SeedResource(seed, 2, trainer.UserId);
            var shift = await SeedShift(seed, resource, owner.UserId);

            using var context = _fixture.CreateContext();
            var result = await CreateService(context, admin.UserId, isAdmin: true)
                .Change(shift.EventResourceUserId, new ChangeShiftRequest { UserId = other.UserId, TrainingCompleted = true });

            var training = await GetTraining(other.UserId, resource.ResourceTypeId);
            Assert.True(training!.TrainingComplete);
            Assert.Equal(admin.UserId, training.ConfirmedBy);
            Assert.NotNull(training.Confirmed);
            Assert.True(result.ChangedTraining!.TrainingComplete);
            Assert.Equal(admin.UserId, result.ChangedTraining.ConfirmedById);
            await _smsSender.DidNotReceiveWithAnyArgs().SendMessages(default!);
        }

        [Fact]
        public async Task Change_Owner_WithoutTrainingRow_AnswerIsOptional()
        {
            using var seed = _fixture.CreateContext();
            var trainer = await SeedUser(seed, "Trener");
            var owner = await SeedUser(seed);
            var resource = await SeedResource(seed, 2, trainer.UserId);
            var shift = await SeedShift(seed, resource, owner.UserId);

            // Ingen flytting, så svaret trengs ikke: eieren av en gammel vakt uten opplæringsrad kan endre kommentaren.
            using (var context = _fixture.CreateContext())
            {
                var result = await CreateService(context, owner.UserId)
                    .Change(shift.EventResourceUserId, new ChangeShiftRequest { Comment = "Ny" });
                Assert.Null(result.ChangedTraining);
            }
            Assert.Null(await GetTraining(owner.UserId, resource.ResourceTypeId));
            Assert.Equal("Ny", Assert.Single(await GetShifts(resource.EventResourceId)).Comment);

            // Sendes svaret likevel, lagres det sammen med endringen.
            using (var context = _fixture.CreateContext())
            {
                var result = await CreateService(context, owner.UserId)
                    .Change(shift.EventResourceUserId, new ChangeShiftRequest { Comment = "Trenger opplæring", TrainingCompleted = false });
                Assert.False(result.ChangedTraining!.TrainingComplete);
                Assert.True(Assert.Single(result.Resource.Shifts).NeedsTraining);
            }
            var training = await GetTraining(owner.UserId, resource.ResourceTypeId);
            Assert.False(training!.TrainingComplete);
            Assert.Null(training.ConfirmedBy);
            Assert.Equal("Trenger opplæring", Assert.Single(await GetShifts(resource.EventResourceId)).Comment);
            await _smsSender.ReceivedWithAnyArgs(1).SendMessages(default!);
        }

        [Theory]
        [InlineData(true, null)]
        [InlineData(false, null)]
        [InlineData(true, true)]
        [InlineData(false, false)]
        public async Task Change_Owner_WithTraining_AnswerIsNullOrSame_ChangesOnlyShift(bool stored, bool? answer)
        {
            using var seed = _fixture.CreateContext();
            var trainer = await SeedUser(seed, "Trener");
            var owner = await SeedUser(seed);
            var resource = await SeedResource(seed, 2, trainer.UserId);
            await SeedTraining(seed, owner.UserId, resource.ResourceTypeId, trainingComplete: stored);
            var shift = await SeedShift(seed, resource, owner.UserId);

            using var context = _fixture.CreateContext();
            var result = await CreateService(context, owner.UserId)
                .Change(shift.EventResourceUserId, new ChangeShiftRequest { Comment = "Ny", TrainingCompleted = answer });

            Assert.Equal("Ny", Assert.Single(await GetShifts(resource.EventResourceId)).Comment);
            var training = await GetTraining(owner.UserId, resource.ResourceTypeId);
            Assert.Equal(stored, training!.TrainingComplete);
            Assert.Null(training.Confirmed); // ikke bekreftet på nytt
            Assert.Null(result.ChangedTraining);
            await _smsSender.DidNotReceiveWithAnyArgs().SendMessages(default!);
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public async Task Change_Owner_WithTraining_DifferentAnswer_UpdatesShiftAndTraining(bool stored, bool answer)
        {
            using var seed = _fixture.CreateContext();
            var trainer = await SeedUser(seed, "Trener");
            var owner = await SeedUser(seed);
            var resource = await SeedResource(seed, 2, trainer.UserId);
            await SeedTraining(seed, owner.UserId, resource.ResourceTypeId, trainingComplete: stored);
            var shift = await SeedShift(seed, resource, owner.UserId);

            using var context = _fixture.CreateContext();
            var result = await CreateService(context, owner.UserId)
                .Change(shift.EventResourceUserId, new ChangeShiftRequest { Comment = "Ny", TrainingCompleted = answer });

            Assert.Equal("Ny", Assert.Single(await GetShifts(resource.EventResourceId)).Comment);
            var training = await GetTraining(owner.UserId, resource.ResourceTypeId);
            Assert.Equal(answer, training!.TrainingComplete);
            Assert.Equal(answer ? owner.UserId : null, training.ConfirmedBy);
            Assert.Equal(answer, training.Confirmed.HasValue);
            Assert.Equal(training.ResourceTypeTrainingId, result.ChangedTraining!.Id);
            Assert.Equal(answer, result.ChangedTraining.TrainingComplete);
            Assert.Equal(!answer, Assert.Single(result.Resource.Shifts).NeedsTraining);
            await _smsSender.ReceivedWithAnyArgs(answer ? 0 : 1).SendMessages(default!);
        }

        [Fact]
        public async Task Change_Admin_OthersShiftWithoutMoving_ConfirmsOwnersTrainingAsAdmin()
        {
            using var seed = _fixture.CreateContext();
            var trainer = await SeedUser(seed, "Trener");
            var owner = await SeedUser(seed);
            var admin = await SeedUser(seed, "Admin", isAdmin: true);
            var resource = await SeedResource(seed, 2, trainer.UserId);
            await SeedTraining(seed, owner.UserId, resource.ResourceTypeId, trainingComplete: false);
            var shift = await SeedShift(seed, resource, owner.UserId);

            using var context = _fixture.CreateContext();
            var result = await CreateService(context, admin.UserId, isAdmin: true)
                .Change(shift.EventResourceUserId, new ChangeShiftRequest { Comment = "Original", TrainingCompleted = true });

            var training = await GetTraining(owner.UserId, resource.ResourceTypeId);
            Assert.True(training!.TrainingComplete);
            Assert.Equal(admin.UserId, training.ConfirmedBy);
            Assert.Equal(owner.UserId, result.ChangedTraining!.UserId);
            await _smsSender.DidNotReceiveWithAnyArgs().SendMessages(default!);
        }

        [Theory]
        [InlineData(true, null, false)]
        [InlineData(true, true, false)]
        [InlineData(true, false, true)]
        [InlineData(false, true, true)]
        public async Task Change_Admin_MovesShiftToUserWithTraining_UpdatesTrainingOnlyWhenAnswerDiffers(bool stored, bool? answer, bool expectChange)
        {
            using var seed = _fixture.CreateContext();
            var trainer = await SeedUser(seed, "Trener");
            var owner = await SeedUser(seed);
            var other = await SeedUser(seed);
            var admin = await SeedUser(seed, "Admin", isAdmin: true);
            var resource = await SeedResource(seed, 2, trainer.UserId);
            await SeedTraining(seed, other.UserId, resource.ResourceTypeId, trainingComplete: stored);
            var shift = await SeedShift(seed, resource, owner.UserId);

            using var context = _fixture.CreateContext();
            var result = await CreateService(context, admin.UserId, isAdmin: true)
                .Change(shift.EventResourceUserId, new ChangeShiftRequest { UserId = other.UserId, TrainingCompleted = answer });

            Assert.Equal(other.UserId, Assert.Single(await GetShifts(resource.EventResourceId)).UserId);
            var training = await GetTraining(other.UserId, resource.ResourceTypeId);
            Assert.Equal(expectChange ? answer : stored, training!.TrainingComplete);
            Assert.Equal(expectChange && answer == true ? admin.UserId : null, training.ConfirmedBy);
            Assert.Equal(expectChange, result.ChangedTraining is not null);
            // Trenerne varsles bare ved overgang til «ønsker opplæring».
            await _smsSender.ReceivedWithAnyArgs(expectChange && answer == false ? 1 : 0).SendMessages(default!);
            // Eieren før flyttingen får ingen opplæringsrad.
            Assert.Null(await GetTraining(owner.UserId, resource.ResourceTypeId));
        }

        [Fact]
        public async Task Change_Admin_MovesShift_LeavesNothingHalfSaved_WhenSaveFails()
        {
            using var seed = _fixture.CreateContext();
            var trainer = await SeedUser(seed, "Trener");
            var owner = await SeedUser(seed);
            var other = await SeedUser(seed);
            var admin = await SeedUser(seed, "Admin", isAdmin: true);
            var resource = await SeedResource(seed, 5, trainer.UserId);
            var shift = await SeedShift(seed, resource, owner.UserId);

            // Simulerer at den nye eieren settes opp på oppgaven samtidig (etter duplikatsjekken): den unike indeksen
            // stopper lagringen etter at opplæringssvaret er tolket og raden lagt til.
            PlannerDbContext context = null!;
            var interceptor = new BeforeSaveInterceptor(() => context.Database.ExecuteSqlInterpolatedAsync(
                $"insert into EventResourceUsers (EventResourceId, UserId) values ({resource.EventResourceId}, {other.UserId})"));
            context = _fixture.CreateContext(interceptor);
            using var _ = context;

            var ex = await Assert.ThrowsAsync<DomainValidationException>(() => CreateService(context, admin.UserId, isAdmin: true)
                .Change(shift.EventResourceUserId, new ChangeShiftRequest { UserId = other.UserId, Comment = "Flyttet", TrainingCompleted = false }));

            Assert.Equal(ShiftService.DuplicateMessage, ex.Message);
            var dbShift = Assert.Single(await GetShifts(resource.EventResourceId));
            Assert.Equal(owner.UserId, dbShift.UserId);
            Assert.Equal("Original", dbShift.Comment);
            Assert.Null(await GetTraining(other.UserId, resource.ResourceTypeId));
            await _smsSender.DidNotReceiveWithAnyArgs().SendMessages(default!);
        }

        [Fact]
        public async Task Change_Owner_UpdatingTraining_LeavesNothingHalfSaved_WhenSaveFails()
        {
            using var seed = _fixture.CreateContext();
            var trainer = await SeedUser(seed, "Trener");
            var owner = await SeedUser(seed);
            var resource = await SeedResource(seed, 2, trainer.UserId);
            await SeedTraining(seed, owner.UserId, resource.ResourceTypeId, trainingComplete: true);
            var shift = await SeedShift(seed, resource, owner.UserId);

            using var context = _fixture.CreateContext(new BeforeSaveInterceptor(() => throw new InvalidOperationException("Databasen er nede")));
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService(context, owner.UserId)
                .Change(shift.EventResourceUserId, new ChangeShiftRequest { Comment = "Ny", TrainingCompleted = false }));

            Assert.Equal("Original", Assert.Single(await GetShifts(resource.EventResourceId)).Comment);
            Assert.True((await GetTraining(owner.UserId, resource.ResourceTypeId))!.TrainingComplete);
            await _smsSender.DidNotReceiveWithAnyArgs().SendMessages(default!);
        }

        [Fact]
        public async Task Change_RetriesOnce_WhenTrainingIsCreatedConcurrently()
        {
            using var seed = _fixture.CreateContext();
            var trainer = await SeedUser(seed, "Trener");
            var owner = await SeedUser(seed);
            var other = await SeedUser(seed);
            var admin = await SeedUser(seed, "Admin", isAdmin: true);
            var resource = await SeedResource(seed, 2, trainer.UserId);
            var shift = await SeedShift(seed, resource, owner.UserId);

            using var context = _fixture.CreateContext(ConcurrentTrainingInserter(other.UserId, resource.ResourceTypeId));
            var result = await CreateService(context, admin.UserId, isAdmin: true)
                .Change(shift.EventResourceUserId, new ChangeShiftRequest { UserId = other.UserId, Comment = "Flyttet", TrainingCompleted = false });

            var dbShift = Assert.Single(await GetShifts(resource.EventResourceId));
            Assert.Equal(other.UserId, dbShift.UserId);
            Assert.Equal("Flyttet", dbShift.Comment);
            // Andre forsøk ser raden fra den samtidige forespørselen (fullført) og oppdaterer den, siden svaret er ulikt.
            using var verify = _fixture.CreateContext();
            var training = Assert.Single(await verify.ResourceTypeTrainings.AsNoTracking()
                .Where(t => t.UserId == other.UserId && t.ResourceTypeId == resource.ResourceTypeId).ToListAsync());
            Assert.False(training.TrainingComplete);
            Assert.Equal(training.ResourceTypeTrainingId, result.ChangedTraining!.Id);
            await _smsSender.ReceivedWithAnyArgs(1).SendMessages(default!);
        }

        #endregion

        #region SetTraining

        [Fact]
        public async Task SetTraining_Owner_RequestsTraining_SendsSmsOnce()
        {
            using var seed = _fixture.CreateContext();
            var trainer = await SeedUser(seed, "Trener");
            var owner = await SeedUser(seed);
            var resource = await SeedResource(seed, 2, trainer.UserId);
            await SeedTraining(seed, owner.UserId, resource.ResourceTypeId, trainingComplete: true);
            var shift = await SeedShift(seed, resource, owner.UserId);

            using (var context = _fixture.CreateContext())
            {
                var result = await CreateService(context, owner.UserId).SetTraining(shift.EventResourceUserId, new SetTrainingRequest { TrainingCompleted = false });
                Assert.False(result.ChangedTraining!.TrainingComplete);
                Assert.True(Assert.Single(result.Resource.Shifts).NeedsTraining);
            }

            // Ønsket er allerede registrert, så trenerne varsles ikke på nytt.
            using (var context = _fixture.CreateContext())
            {
                await CreateService(context, owner.UserId).SetTraining(shift.EventResourceUserId, new SetTrainingRequest { TrainingCompleted = false });
            }

            var training = await GetTraining(owner.UserId, resource.ResourceTypeId);
            Assert.False(training!.TrainingComplete);
            Assert.Null(training.Confirmed);
            Assert.Null(training.ConfirmedBy);
            await _smsSender.Received(1).SendMessages(Arg.Any<IEnumerable<SmsMessage>>());
        }

        [Fact]
        public async Task SetTraining_Trainer_ConfirmsOwnersTraining()
        {
            using var seed = _fixture.CreateContext();
            var trainer = await SeedUser(seed, "Trener");
            var owner = await SeedUser(seed);
            var resource = await SeedResource(seed, 2, trainer.UserId);
            await SeedTraining(seed, owner.UserId, resource.ResourceTypeId, trainingComplete: false);
            var shift = await SeedShift(seed, resource, owner.UserId);

            using var context = _fixture.CreateContext();
            var result = await CreateService(context, trainer.UserId).SetTraining(shift.EventResourceUserId, new SetTrainingRequest { TrainingCompleted = true });

            var training = await GetTraining(owner.UserId, resource.ResourceTypeId);
            Assert.True(training!.TrainingComplete);
            Assert.Equal(trainer.UserId, training.ConfirmedBy);
            Assert.NotNull(training.Confirmed);

            Assert.Equal(owner.UserId, result.ChangedTraining!.UserId);
            Assert.Equal(trainer.UserId, result.ChangedTraining.ConfirmedById);
            Assert.Equal("Trener Bruker", result.ChangedTraining.ConfirmedByName);
            var responseShift = Assert.Single(result.Resource.Shifts);
            Assert.False(responseShift.NeedsTraining);
            Assert.False(responseShift.CanConfirmTraining);
            Assert.Equal("Original", responseShift.Comment);
            await _smsSender.DidNotReceiveWithAnyArgs().SendMessages(default!);
        }

        [Fact]
        public async Task SetTraining_Admin_CreatesTrainingForOwner()
        {
            using var seed = _fixture.CreateContext();
            var trainer = await SeedUser(seed, "Trener");
            var owner = await SeedUser(seed);
            var admin = await SeedUser(seed, "Admin", isAdmin: true);
            var resource = await SeedResource(seed, 2, trainer.UserId);
            var shift = await SeedShift(seed, resource, owner.UserId);

            using var context = _fixture.CreateContext();
            var result = await CreateService(context, admin.UserId, isAdmin: true, now: AfterResourceInNorway)
                .SetTraining(shift.EventResourceUserId, new SetTrainingRequest { TrainingCompleted = true });

            var training = await GetTraining(owner.UserId, resource.ResourceTypeId);
            Assert.True(training!.TrainingComplete);
            Assert.Equal(admin.UserId, training.ConfirmedBy);
            // Opplæringen gjelder eieren av vakta, ikke admin som satte den.
            Assert.Equal(owner.UserId, result.ChangedTraining!.UserId);
        }

        [Fact]
        public async Task SetTraining_ThrowsForbidden_WhenOtherUser()
        {
            using var seed = _fixture.CreateContext();
            var trainer = await SeedUser(seed, "Trener");
            var owner = await SeedUser(seed);
            var other = await SeedUser(seed);
            var resource = await SeedResource(seed, 2, trainer.UserId);
            var shift = await SeedShift(seed, resource, owner.UserId);

            using var context = _fixture.CreateContext();
            await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateService(context, other.UserId)
                .SetTraining(shift.EventResourceUserId, new SetTrainingRequest { TrainingCompleted = true }));
            Assert.Null(await GetTraining(owner.UserId, resource.ResourceTypeId));
        }

        [Fact]
        public async Task SetTraining_ThrowsForbidden_WhenTrainerForOtherResourceType()
        {
            using var seed = _fixture.CreateContext();
            var trainer = await SeedUser(seed, "Trener");
            var otherTrainer = await SeedUser(seed, "Annen");
            var owner = await SeedUser(seed);
            var resource = await SeedResource(seed, 2, trainer.UserId);
            await SeedResource(seed, 2, otherTrainer.UserId);
            var shift = await SeedShift(seed, resource, owner.UserId);

            using var context = _fixture.CreateContext();
            await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateService(context, otherTrainer.UserId)
                .SetTraining(shift.EventResourceUserId, new SetTrainingRequest { TrainingCompleted = true }));
        }

        [Fact]
        public async Task SetTraining_Trainer_ThrowsDomainValidation_WhenResourceHasEnded()
        {
            using var seed = _fixture.CreateContext();
            var trainer = await SeedUser(seed, "Trener");
            var owner = await SeedUser(seed);
            var resource = await SeedResource(seed, 2, trainer.UserId);
            var shift = await SeedShift(seed, resource, owner.UserId);

            using var context = _fixture.CreateContext();
            var ex = await Assert.ThrowsAsync<DomainValidationException>(() => CreateService(context, trainer.UserId, now: AfterResourceInNorway)
                .SetTraining(shift.EventResourceUserId, new SetTrainingRequest { TrainingCompleted = true }));
            Assert.Equal(ShiftService.PastMessage, ex.Message);
        }

        [Fact]
        public async Task SetTraining_ThrowsDomainValidation_WhenResourceTypeHasNoTraining()
        {
            using var seed = _fixture.CreateContext();
            var owner = await SeedUser(seed);
            var resource = await SeedResource(seed);
            var shift = await SeedShift(seed, resource, owner.UserId);

            using var context = _fixture.CreateContext();
            var ex = await Assert.ThrowsAsync<DomainValidationException>(() => CreateService(context, owner.UserId)
                .SetTraining(shift.EventResourceUserId, new SetTrainingRequest { TrainingCompleted = false }));
            Assert.Equal(ShiftService.NoTrainingMessage, ex.Message);
        }


        [Fact]
        public async Task SetTraining_RetriesOnce_WhenTrainingIsCreatedConcurrently_AndUpdatesIt()
        {
            using var seed = _fixture.CreateContext();
            var trainer = await SeedUser(seed, "Trener");
            var owner = await SeedUser(seed);
            var resource = await SeedResource(seed, 2, trainer.UserId);
            var shift = await SeedShift(seed, resource, owner.UserId);

            using var context = _fixture.CreateContext(ConcurrentTrainingInserter(owner.UserId, resource.ResourceTypeId));
            var result = await CreateService(context, owner.UserId)
                .SetTraining(shift.EventResourceUserId, new SetTrainingRequest { TrainingCompleted = false });

            using var verify = _fixture.CreateContext();
            var training = Assert.Single(await verify.ResourceTypeTrainings.AsNoTracking()
                .Where(t => t.UserId == owner.UserId && t.ResourceTypeId == resource.ResourceTypeId).ToListAsync());
            Assert.False(training.TrainingComplete);
            Assert.Equal(training.ResourceTypeTrainingId, result.ChangedTraining!.Id);
            await _smsSender.Received(1).SendMessages(Arg.Any<IEnumerable<SmsMessage>>());
        }

        #endregion

        #region Withdraw

        [Fact]
        public async Task Withdraw_Owner_RemovesShift()
        {
            using var seed = _fixture.CreateContext();
            var owner = await SeedUser(seed);
            var resource = await SeedResource(seed);
            var shift = await SeedShift(seed, resource, owner.UserId);

            using var context = _fixture.CreateContext();
            var result = await CreateService(context, owner.UserId).Withdraw(shift.EventResourceUserId);

            Assert.Empty(await GetShifts(resource.EventResourceId));
            Assert.Empty(result.Resource.Shifts);
            Assert.True(result.Resource.CanSignUp);
        }

        [Fact]
        public async Task Withdraw_ThrowsForbidden_WhenOtherUser()
        {
            using var seed = _fixture.CreateContext();
            var owner = await SeedUser(seed);
            var trainer = await SeedUser(seed, "Trener");
            var resource = await SeedResource(seed, 2, trainer.UserId);
            var shift = await SeedShift(seed, resource, owner.UserId);

            using var context = _fixture.CreateContext();
            await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateService(context, trainer.UserId).Withdraw(shift.EventResourceUserId));
            Assert.Single(await GetShifts(resource.EventResourceId));
        }

        [Fact]
        public async Task Withdraw_Owner_ThrowsDomainValidation_WhenResourceHasEnded_ButAdminCan()
        {
            using var seed = _fixture.CreateContext();
            var owner = await SeedUser(seed);
            var admin = await SeedUser(seed, "Admin", isAdmin: true);
            var resource = await SeedResource(seed);
            var shift = await SeedShift(seed, resource, owner.UserId);

            using (var context = _fixture.CreateContext())
            {
                await Assert.ThrowsAsync<DomainValidationException>(
                    () => CreateService(context, owner.UserId, now: AfterResourceInNorway).Withdraw(shift.EventResourceUserId));
            }
            Assert.Single(await GetShifts(resource.EventResourceId));

            using (var context = _fixture.CreateContext())
            {
                await CreateService(context, admin.UserId, isAdmin: true, now: AfterResourceInNorway).Withdraw(shift.EventResourceUserId);
            }
            Assert.Empty(await GetShifts(resource.EventResourceId));
        }

        [Fact]
        public async Task Withdraw_ThrowsEntityNotFound_WhenShiftDoesNotExist()
        {
            using var seed = _fixture.CreateContext();
            var user = await SeedUser(seed);

            using var context = _fixture.CreateContext();
            await Assert.ThrowsAsync<EntityNotFoundException>(() => CreateService(context, user.UserId).Withdraw(999999));
        }

        #endregion

        #region Ledige vakter

        private async Task<int> GetShiftCount(int resourceId)
        {
            using var verify = _fixture.CreateContext();
            return await verify.EventResource.AsNoTracking().Where(r => r.EventResourceId == resourceId).Select(r => r.ShiftCount).SingleAsync();
        }

        [Fact]
        public async Task AddEmptySlot_Admin_IncreasesOnFullResource_ReturnsResourceThatIsNoLongerFull()
        {
            using var seed = _fixture.CreateContext();
            var first = await SeedUser(seed);
            var user = await SeedUser(seed);
            var admin = await SeedUser(seed, "Admin", isAdmin: true);
            var resource = await SeedResource(seed, shiftCount: 1);
            await SeedShift(seed, resource, first.UserId);

            using var context = _fixture.CreateContext();
            var result = await CreateService(context, admin.UserId, isAdmin: true).AddEmptySlot(resource.EventResourceId);

            Assert.Equal(2, await GetShiftCount(resource.EventResourceId));
            Assert.Equal(resource.EventResourceId, result.Id);
            Assert.Equal(2, result.ShiftCount);
            Assert.False(result.IsFull);
            Assert.True(result.IsMissingStaff);
            Assert.True(result.CanSignUp);
            Assert.Single(result.Shifts);

            // Samme lesemodul som GET api/events: en vanlig bruker kan nå ta vakt.
            using var userContext = _fixture.CreateContext();
            var reader = new ResourceReader(userContext, new FakeTimeProvider(BeforeResource));
            var forUser = (await reader.GetResource(new Actor(user.UserId, IsAdmin: false), resource.EventResourceId))!;
            Assert.False(forUser.IsFull);
            Assert.True(forUser.CanSignUp);
        }

        [Fact]
        public async Task AddEmptySlot_IncreasesByOne_WhenResourceHasEmptySlots()
        {
            using var seed = _fixture.CreateContext();
            var admin = await SeedUser(seed, "Admin", isAdmin: true);
            var resource = await SeedResource(seed, shiftCount: 3);

            using var context = _fixture.CreateContext();
            var result = await CreateService(context, admin.UserId, isAdmin: true).AddEmptySlot(resource.EventResourceId);

            Assert.Equal(4, result.ShiftCount);
            Assert.Equal(4, await GetShiftCount(resource.EventResourceId));
        }

        [Fact]
        public async Task AddEmptySlot_CountsFromShifts_WhenResourceIsOverbooked()
        {
            using var seed = _fixture.CreateContext();
            var a = await SeedUser(seed);
            var b = await SeedUser(seed);
            var c = await SeedUser(seed);
            var admin = await SeedUser(seed, "Admin", isAdmin: true);
            var resource = await SeedResource(seed, shiftCount: 1);
            await SeedShift(seed, resource, a.UserId);
            await SeedShift(seed, resource, b.UserId);
            await SeedShift(seed, resource, c.UserId);

            using var context = _fixture.CreateContext();
            var result = await CreateService(context, admin.UserId, isAdmin: true).AddEmptySlot(resource.EventResourceId);

            // Tre vakter og én ny ledig vakt.
            Assert.Equal(4, result.ShiftCount);
            Assert.True(result.IsMissingStaff);
            Assert.Equal(4, await GetShiftCount(resource.EventResourceId));
        }

        /// <summary>
        /// Kjører <paramref name="action"/> to ganger, A og B, med hver sin kontekst, styrt slik at B starter mens A er inne
        /// i oppgavelåsen og har lest bemanningen, men ikke skrevet ny verdi (<see cref="BeforeWriteAfterReadInterceptor"/>).
        /// A fortsetter først når B enten står og venter på en lås (låsen virker) eller er ferdig (ingen lås: B leste den
        /// samme gamle verdien og skrev før A, slik at A overskriver B).
        /// </summary>
        /// <returns>Unntaket fra A og B (<c>null</c> hvis kallet lyktes), og om B ble ferdig mens A var inne i låsen.</returns>
        private Task<(Exception? First, Exception? Second, bool SecondFinishedInsideFirst)> RunInterleaved(
            int adminUserId, Func<IShiftService, Task> action)
            => RunInterleaved(
                inside => new BeforeWriteAfterReadInterceptor(inside),
                contextA => action(CreateService(contextA, adminUserId, isAdmin: true)),
                contextB => action(CreateService(contextB, adminUserId, isAdmin: true)));

        /// <summary>
        /// Som <see cref="RunInterleaved(int, Func{IShiftService, Task})"/>, men med ulike operasjoner for A og B, og der
        /// <paramref name="pauseFirst"/> bestemmer hvor A stopper (interceptoren får tilbakekallet som starter B og venter).
        /// </summary>
        private async Task<(Exception? First, Exception? Second, bool SecondFinishedInsideFirst)> RunInterleaved(
            Func<Func<Task>, IInterceptor> pauseFirst, Func<PlannerDbContext, Task> first, Func<PlannerDbContext, Task> second)
        {
            // B sin tilkobling åpnes og varmes opp på forhånd, så B kan lese med en gang den startes. Ellers kan en treg
            // tilkobling gjøre at A rekker å committe før B leser, og testen blir grønn selv uten lås.
            using var contextB = _fixture.CreateContext();
            await contextB.Database.OpenConnectionAsync();
            var sessionB = await GetSessionId(contextB);

            using var monitor = _fixture.CreateContext();
            await monitor.Database.OpenConnectionAsync();
            await IsWaitingForLock(monitor, sessionB);

            Task<Exception?>? secondTask = null;
            var secondFinishedInsideFirst = false;

            using var contextA = _fixture.CreateContext(pauseFirst(async () =>
            {
                secondTask = Task.Run(() => Attempt(() => second(contextB)));
                var timeout = Task.Delay(TimeSpan.FromSeconds(5));
                while (!secondTask.IsCompleted && !timeout.IsCompleted && !await IsWaitingForLock(monitor, sessionB))
                    await Task.Delay(10);
                secondFinishedInsideFirst = secondTask.IsCompleted;
            }));
            var firstResult = await Attempt(() => first(contextA));

            Assert.NotNull(secondTask); // interceptoren kjørte, B ble startet inne i A
            return (firstResult, await secondTask, secondFinishedInsideFirst);
        }

        private static async Task<short> GetSessionId(PlannerDbContext context)
            => await context.Database.SqlQuery<short>($"SELECT @@SPID AS [Value]").SingleAsync();

        // SQL Server-spesifikt (testene kjører mot MSSQL i Testcontainers): sesjonen venter på en lås (LCK_M_*).
        private static async Task<bool> IsWaitingForLock(PlannerDbContext monitor, short sessionId)
            => await monitor.Database.SqlQuery<int>(
                $"SELECT COUNT(*) AS [Value] FROM sys.dm_exec_requests WHERE session_id = {sessionId} AND wait_type LIKE 'LCK%'")
                .SingleAsync() > 0;

        private static EventsService CreateEventsService(PlannerDbContext context, int adminUserId)
        {
            var currentUser = Substitute.For<ICurrentUserService>();
            currentUser.UserId.Returns(adminUserId);
            currentUser.IsAdmin.Returns(true);
            return new EventsService(context, new ResourceReader(context, new FakeTimeProvider(BeforeResource)), currentUser);
        }

        /// <summary>Vaktlisteskjemaet for vaktlista til <paramref name="resource"/>, med én oppgave.</summary>
        private static EventRequest EventFormRequest(Event evt, EventResource resource, int shiftCount, int? originalShiftCount, TimeOnly? resourceStart = null)
            => new()
            {
                Name = evt.Name,
                StartTime = evt.StartTime,
                EndTime = evt.EndTime,
                Resources =
                [
                    new ResourceRequest
                    {
                        Id = resource.EventResourceId,
                        ResourceTypeId = resource.ResourceTypeId,
                        StartTime = resourceStart ?? TimeOnly.FromDateTime(ResourceStart),
                        EndTime = TimeOnly.FromDateTime(ResourceEnd),
                        ShiftCount = shiftCount,
                        OriginalShiftCount = originalShiftCount,
                    },
                ],
            };

        /// <summary>
        /// #151: admin A lagrer vaktlisteskjemaet (3 → 6) mens admin B klikker «Legg til ledig vakt». B startes mens A er inne
        /// i transaksjonen og har lest bemanningen, men ikke lagret (<see cref="BeforeSaveInterceptor"/>). B venter på
        /// oppgavelåsen og legger den ledige vakta til på A sin verdi, så begge endringene teller. Uten låsen ville B skrevet 4 inne i A,
        /// og A overskrevet med 6.
        /// </summary>
        [Fact]
        public async Task UpdateEvent_ThenAddEmptySlot_Concurrent_BothChangesCount()
        {
            using var seed = _fixture.CreateContext();
            var admin = await SeedUser(seed, "Admin", isAdmin: true);
            var resource = await SeedResource(seed, shiftCount: 3);
            var evt = await seed.Events.AsNoTracking().SingleAsync(e => e.EventId == resource.EventId);

            var (first, second, secondFinishedInsideFirst) = await RunInterleaved(
                inside => new BeforeSaveInterceptor(inside),
                contextA => CreateEventsService(contextA, admin.UserId)
                    .UpdateEvent(evt.EventId, EventFormRequest(evt, resource, shiftCount: 6, originalShiftCount: 3)),
                contextB => CreateService(contextB, admin.UserId, isAdmin: true).AddEmptySlot(resource.EventResourceId));

            Assert.Null(first);
            Assert.Null(second);
            Assert.False(secondFinishedInsideFirst); // B ventet på oppgavelåsen
            Assert.Equal(7, await GetShiftCount(resource.EventResourceId));
        }

        /// <summary>
        /// #151, motsatt rekkefølge: admin A klikker «Legg til ledig vakt» (3 → 4), og admin B lagrer skjemaet (lastet med 3,
        /// satt til 6) mens A er inne i oppgavelåsen. B venter på låsen, ser deretter at verdien er endret av noen andre og
        /// får konflikt i stedet for å overskrive A sin ledige vakt i stillhet.
        /// </summary>
        [Fact]
        public async Task AddEmptySlot_ThenUpdateEvent_Concurrent_UpdateEventGetsConflict()
        {
            using var seed = _fixture.CreateContext();
            var admin = await SeedUser(seed, "Admin", isAdmin: true);
            var resource = await SeedResource(seed, shiftCount: 3);
            var evt = await seed.Events.AsNoTracking().SingleAsync(e => e.EventId == resource.EventId);

            var (first, second, secondFinishedInsideFirst) = await RunInterleaved(
                inside => new BeforeWriteAfterReadInterceptor(inside),
                contextA => CreateService(contextA, admin.UserId, isAdmin: true).AddEmptySlot(resource.EventResourceId),
                contextB => CreateEventsService(contextB, admin.UserId)
                    .UpdateEvent(evt.EventId, EventFormRequest(evt, resource, shiftCount: 6, originalShiftCount: 3)));

            Assert.Null(first);
            Assert.IsType<ConcurrentUpdateException>(second);
            Assert.False(secondFinishedInsideFirst); // B ventet på oppgavelåsen
            Assert.Equal(4, await GetShiftCount(resource.EventResourceId));
        }

        /// <summary>
        /// Admin A flytter starten på oppgaven (09:00 → 10:00) mens en bruker tar vakt fra 09:00. Endringen i tider skjer under
        /// oppgavelåsen, så påmeldingen venter og vurderes mot de nye tidene. Uten låsen ville den blitt godkjent mot de gamle.
        /// </summary>
        [Fact]
        public async Task UpdateEvent_ChangingTimes_BlocksConcurrentSignUp_WhichIsCheckedAgainstNewTimes()
        {
            using var seed = _fixture.CreateContext();
            var admin = await SeedUser(seed, "Admin", isAdmin: true);
            var user = await SeedUser(seed);
            var resource = await SeedResource(seed, shiftCount: 3);
            var evt = await seed.Events.AsNoTracking().SingleAsync(e => e.EventId == resource.EventId);

            var (first, second, secondFinishedInsideFirst) = await RunInterleaved(
                inside => new BeforeSaveInterceptor(inside),
                contextA => CreateEventsService(contextA, admin.UserId)
                    .UpdateEvent(evt.EventId, EventFormRequest(evt, resource, shiftCount: 3, originalShiftCount: 3, resourceStart: new TimeOnly(10, 0))),
                contextB => CreateService(contextB, user.UserId)
                    .SignUp(resource.EventResourceId, new SignUpRequest { StartTime = ResourceStart, EndTime = ResourceEnd }));

            Assert.Null(first);
            var ex = Assert.IsType<DomainValidationException>(second);
            Assert.Equal(ShiftService.InvalidTimesMessage, ex.Message);
            Assert.False(secondFinishedInsideFirst); // B ventet på oppgavelåsen
            Assert.Empty(await GetShifts(resource.EventResourceId));
        }

        [Fact]
        public async Task AddEmptySlot_Concurrent_BothClicksCount()
        {
            using var seed = _fixture.CreateContext();
            var admin = await SeedUser(seed, "Admin", isAdmin: true);
            var resource = await SeedResource(seed, shiftCount: 3);

            var (first, second, secondFinishedInsideFirst) = await RunInterleaved(
                admin.UserId, service => service.AddEmptySlot(resource.EventResourceId));

            Assert.Equal(5, await GetShiftCount(resource.EventResourceId));
            Assert.Null(first);
            Assert.Null(second);
            Assert.False(secondFinishedInsideFirst); // B ventet på oppgavelåsen
        }

        [Fact]
        public async Task RemoveEmptySlot_Concurrent_BothClicksCount()
        {
            using var seed = _fixture.CreateContext();
            var admin = await SeedUser(seed, "Admin", isAdmin: true);
            var resource = await SeedResource(seed, shiftCount: 3);

            var (first, second, secondFinishedInsideFirst) = await RunInterleaved(
                admin.UserId, service => service.RemoveEmptySlot(resource.EventResourceId));

            Assert.Equal(1, await GetShiftCount(resource.EventResourceId));
            Assert.Null(first);
            Assert.Null(second);
            Assert.False(secondFinishedInsideFirst); // B ventet på oppgavelåsen
        }

        [Fact]
        public async Task RemoveEmptySlot_Concurrent_OnlyOneRemovesTheLastEmptySlot()
        {
            using var seed = _fixture.CreateContext();
            var user = await SeedUser(seed);
            var admin = await SeedUser(seed, "Admin", isAdmin: true);
            var resource = await SeedResource(seed, shiftCount: 2);
            await SeedShift(seed, resource, user.UserId);

            var (first, second, secondFinishedInsideFirst) = await RunInterleaved(
                admin.UserId, service => service.RemoveEmptySlot(resource.EventResourceId));

            Assert.Null(first);
            var ex = Assert.IsType<DomainValidationException>(second);
            Assert.Equal(ShiftService.NoEmptySlotMessage, ex.Message);
            Assert.False(secondFinishedInsideFirst);
            Assert.Equal(1, await GetShiftCount(resource.EventResourceId));
        }

        [Fact]
        public async Task RemoveEmptySlot_Admin_DecreasesToStaffedCount_ReturnsFullResource()
        {
            using var seed = _fixture.CreateContext();
            var first = await SeedUser(seed);
            var admin = await SeedUser(seed, "Admin", isAdmin: true);
            var resource = await SeedResource(seed, shiftCount: 2);
            await SeedShift(seed, resource, first.UserId);

            using var context = _fixture.CreateContext();
            var result = await CreateService(context, admin.UserId, isAdmin: true).RemoveEmptySlot(resource.EventResourceId);

            Assert.Equal(1, result.ShiftCount);
            Assert.Equal(1, await GetShiftCount(resource.EventResourceId));
            Assert.True(result.IsFull);
            Assert.False(result.IsMissingStaff);
            Assert.True(result.CanSignUp); // admin kan overbooke
        }

        [Theory]
        // Kolonner: antall vakter, bemannede vakter
        [InlineData(1, 1)]
        [InlineData(1, 2)]
        [InlineData(0, 0)]
        public async Task RemoveEmptySlot_ThrowsDomainValidation_WhenNoEmptySlot(int shiftCount, int staffedCount)
        {
            using var seed = _fixture.CreateContext();
            var admin = await SeedUser(seed, "Admin", isAdmin: true);
            var resource = await SeedResource(seed, shiftCount: shiftCount);
            for (var i = 0; i < staffedCount; i++)
                await SeedShift(seed, resource, (await SeedUser(seed)).UserId);

            using var context = _fixture.CreateContext();
            var ex = await Assert.ThrowsAsync<DomainValidationException>(
                () => CreateService(context, admin.UserId, isAdmin: true).RemoveEmptySlot(resource.EventResourceId));
            Assert.Equal(ShiftService.NoEmptySlotMessage, ex.Message);
            Assert.Equal(shiftCount, await GetShiftCount(resource.EventResourceId));
        }

        [Fact]
        public async Task EmptySlots_ThrowForbidden_WhenNotAdmin()
        {
            using var seed = _fixture.CreateContext();
            var user = await SeedUser(seed);
            var resource = await SeedResource(seed, shiftCount: 2);

            using var context = _fixture.CreateContext();
            var service = CreateService(context, user.UserId);
            await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.AddEmptySlot(resource.EventResourceId));
            await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.RemoveEmptySlot(resource.EventResourceId));
            Assert.Equal(2, await GetShiftCount(resource.EventResourceId));
        }

        [Fact]
        public async Task EmptySlots_ThrowEntityNotFound_WhenResourceDoesNotExist()
        {
            using var seed = _fixture.CreateContext();
            var admin = await SeedUser(seed, "Admin", isAdmin: true);

            using var context = _fixture.CreateContext();
            var service = CreateService(context, admin.UserId, isAdmin: true);
            await Assert.ThrowsAsync<EntityNotFoundException>(() => service.AddEmptySlot(999999));
            await Assert.ThrowsAsync<EntityNotFoundException>(() => service.RemoveEmptySlot(999999));
        }

        #endregion

        #region Kompetanseadvarsler

        [Theory]
        // Kolonner: godkjent, utløpsdato (dager fra «nå», null = ingen), forventet advarsel
        [InlineData(true, null, false)]
        [InlineData(true, 30, false)]
        [InlineData(true, -1, true)]
        [InlineData(true, 0, true)] // utløper akkurat nå = utløpt
        [InlineData(false, null, true)]
        public async Task CompetencyWarnings_CountOnlyValidCompetencies(bool approved, int? expiresInDays, bool expectWarning)
        {
            using var seed = _fixture.CreateContext();
            var user = await SeedUser(seed);
            var resource = await SeedResource(seed);
            var competency = new Competency { Name = UniqueName("Førstehjelp") };
            seed.Competencies.Add(competency);
            await seed.SaveChangesAsync();
            seed.ResourceTypeCompetencies.Add(new ResourceTypeCompetency { ResourceTypeId = resource.ResourceTypeId, CompetencyId = competency.CompetencyId, MinimumRequired = 1 });
            seed.UserCompetencies.Add(new UserCompetency
            {
                UserId = user.UserId,
                CompetencyId = competency.CompetencyId,
                Approved = approved,
                ExpiryDate = expiresInDays is { } days ? BeforeResource.UtcDateTime.AddDays(days) : null,
                Created = DateTime.UtcNow,
            });
            await seed.SaveChangesAsync();

            using var context = _fixture.CreateContext();
            var result = await CreateService(context, user.UserId).SignUp(resource.EventResourceId, new SignUpRequest());

            if (expectWarning)
            {
                var warning = Assert.Single(result.Resource.CompetencyWarnings);
                Assert.Equal(competency.Name, warning.CompetencyName);
                Assert.Equal(0, warning.CurrentCount);
                Assert.Equal(1, warning.MinimumRequired);
            }
            else
            {
                Assert.Empty(result.Resource.CompetencyWarnings);
            }
        }

        #endregion
    }
}
