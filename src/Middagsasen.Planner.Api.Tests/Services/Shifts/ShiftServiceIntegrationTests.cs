using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services;
using Middagsasen.Planner.Api.Services.ResourceTypes;
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
        // Ressursene i testene er 15.01.2026 09:00–15:00 norsk tid (UTC+1).
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
            var notifier = new TrainerNotifier(context, _smsSender, NullLogger<TrainerNotifier>.Instance);
            return new ShiftService(new ShiftRepository(context), currentUser, notifier, new FakeTimeProvider(now ?? BeforeResource));
        }

        private static string UniqueName(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

        private static async Task<User> SeedUser(PlannerDbContext context, string firstName = "Test", bool isAdmin = false)
        {
            var user = new User
            {
                UserName = UniqueName("user"),
                FirstName = firstName,
                LastName = "Bruker",
                Created = DateTime.UtcNow,
                IsAdmin = isAdmin,
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();
            return user;
        }

        private static async Task<EventResource> SeedResource(PlannerDbContext context, int minimumStaff = 2, params int[] trainerUserIds)
        {
            var resourceType = new ResourceType { Name = UniqueName("Heis"), DefaultStaff = minimumStaff };
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
                        MinimumStaff = minimumStaff,
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
            var resource = await SeedResource(seed, minimumStaff: 2);

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
        public async Task SignUp_NeedsTraining_CreatesTrainingRequest_AndSendsSmsAfterCommit()
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
            var result = await CreateService(context, user.UserId).SignUp(resource.EventResourceId, new SignUpRequest { NeedsTraining = true });

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
            Assert.False(result.ChangedTraining.TrainingComplete);
            Assert.False(result.Resource.MustAnswerTraining);
            Assert.True(Assert.Single(result.Resource.Shifts).NeedsTraining);
            Assert.Empty(result.Warnings);
        }

        [Fact]
        public async Task SignUp_DoesNotNeedTraining_CreatesCompletedTraining_WithoutSms()
        {
            using var seed = _fixture.CreateContext();
            var trainer = await SeedUser(seed, "Trener");
            var user = await SeedUser(seed);
            var resource = await SeedResource(seed, 2, trainer.UserId);

            using var context = _fixture.CreateContext();
            var result = await CreateService(context, user.UserId).SignUp(resource.EventResourceId, new SignUpRequest { NeedsTraining = false });

            var training = await GetTraining(user.UserId, resource.ResourceTypeId);
            Assert.NotNull(training);
            Assert.True(training.TrainingComplete);
            Assert.Equal(user.UserId, training.ConfirmedBy);
            Assert.NotNull(training.Confirmed);
            Assert.True(result.ChangedTraining!.TrainingComplete);
            Assert.False(Assert.Single(result.Resource.Shifts).NeedsTraining);
            await _smsSender.DidNotReceiveWithAnyArgs().SendMessages(default!);
        }

        [Fact]
        public async Task SignUp_IgnoresTrainingAnswer_WhenUserAlreadyHasTraining()
        {
            using var seed = _fixture.CreateContext();
            var trainer = await SeedUser(seed, "Trener");
            var user = await SeedUser(seed);
            var resource = await SeedResource(seed, 2, trainer.UserId);
            await SeedTraining(seed, user.UserId, resource.ResourceTypeId, trainingComplete: true);

            using var context = _fixture.CreateContext();
            var result = await CreateService(context, user.UserId).SignUp(resource.EventResourceId, new SignUpRequest { NeedsTraining = true });

            Assert.True((await GetTraining(user.UserId, resource.ResourceTypeId))!.TrainingComplete);
            Assert.Null(result.ChangedTraining);
            Assert.Single(await GetShifts(resource.EventResourceId));
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
            var result = await CreateService(context, user.UserId).SignUp(resource.EventResourceId, new SignUpRequest { NeedsTraining = true });

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
            var result = await CreateService(context, user.UserId).SignUp(resource.EventResourceId, new SignUpRequest { NeedsTraining = true });

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
                () => CreateService(context, user.UserId).SignUp(resource.EventResourceId, new SignUpRequest { NeedsTraining = true }));

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
            var resource = await SeedResource(seed, minimumStaff: 1);
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
            var resource = await SeedResource(seed, minimumStaff: 1);

            using var contextA = _fixture.CreateContext();
            using var contextB = _fixture.CreateContext();
            var results = await Task.WhenAll(
                Attempt(CreateService(contextA, a.UserId), resource.EventResourceId),
                Attempt(CreateService(contextB, b.UserId), resource.EventResourceId));

            Assert.Single(results, r => r is null);
            Assert.Single(results, r => r is DomainValidationException);
            Assert.Single(await GetShifts(resource.EventResourceId));

            static async Task<Exception?> Attempt(IShiftService service, int resourceId)
            {
                try
                {
                    await service.SignUp(resourceId, new SignUpRequest());
                    return null;
                }
                catch (Exception ex)
                {
                    return ex;
                }
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task SignUp_RejectsDuplicate_AlsoForAdmin(bool isAdmin)
        {
            using var seed = _fixture.CreateContext();
            var user = await SeedUser(seed, isAdmin: isAdmin);
            var resource = await SeedResource(seed, minimumStaff: 5);
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
                .SignUp(resource.EventResourceId, new SignUpRequest { UserId = user.UserId, NeedsTraining = false });

            var training = await GetTraining(user.UserId, resource.ResourceTypeId);
            Assert.True(training!.TrainingComplete);
            Assert.Equal(admin.UserId, training.ConfirmedBy);
            var shift = Assert.Single(result.Resource.Shifts);
            Assert.Equal(user.UserId, shift.User.Id);
            Assert.False(shift.IsMine);
            Assert.True(shift.CanEdit);
            // Admin har selv ikke svart på opplæring for ressurstypen.
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
        public async Task Change_Trainer_ThrowsForbidden()
        {
            using var seed = _fixture.CreateContext();
            var owner = await SeedUser(seed);
            var trainer = await SeedUser(seed, "Trener");
            var resource = await SeedResource(seed, 2, trainer.UserId);
            var shift = await SeedShift(seed, resource, owner.UserId);

            using var context = _fixture.CreateContext();
            await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateService(context, trainer.UserId)
                .Change(shift.EventResourceUserId, new ChangeShiftRequest { Comment = "Trener" }));
            Assert.Equal("Original", Assert.Single(await GetShifts(resource.EventResourceId)).Comment);
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
            var resource = await SeedResource(seed, minimumStaff: 5);
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

            Assert.Equal(trainer.UserId, result.ChangedTraining!.ConfirmedById);
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
            await CreateService(context, admin.UserId, isAdmin: true, now: AfterResourceInNorway)
                .SetTraining(shift.EventResourceUserId, new SetTrainingRequest { TrainingCompleted = true });

            var training = await GetTraining(owner.UserId, resource.ResourceTypeId);
            Assert.True(training!.TrainingComplete);
            Assert.Equal(admin.UserId, training.ConfirmedBy);
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
