using Middagsasen.Planner.Api.Core;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services;
using Middagsasen.Planner.Api.Services.Users;
using Middagsasen.Planner.Api.Tests.Infrastructure;

namespace Middagsasen.Planner.Api.Tests.Services.Users
{
    [Collection("Database")]
    public class UserServiceIntegrationTests
    {
        private readonly DatabaseFixture _fixture;

        public UserServiceIntegrationTests(DatabaseFixture fixture)
        {
            _fixture = fixture;
        }

        private static UserService CreateService(PlannerDbContext context)
            => new UserService(context);

        private static string UniqueName(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

        /// <summary>
        /// Dagens dato i norsk tid. Vakttider lagres som norsk lokal tid, så testdataene må ikke avhenge av
        /// tidssonen til maskinen som kjører testene.
        /// </summary>
        private static DateTime NorwegianToday()
            => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, DateTimeExtensions.SeasonTimeZone).Date;

        private static async Task<User> SeedUser(PlannerDbContext context, string firstName)
        {
            var user = new User
            {
                // Brukernavnet er unikt i databasen, så det må ikke kunne kollidere med andre testers brukere.
                UserName = UniqueName("hof"),
                FirstName = firstName,
                LastName = "HallOfFame",
                Created = DateTime.UtcNow,
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();
            return user;
        }

        /// <summary>
        /// Oppretter et arrangement med to ressurser, som starter og slutter <paramref name="daysFromToday"/> dager fra i dag.
        /// </summary>
        private static async Task<Event> SeedEvent(PlannerDbContext context, int daysFromToday)
        {
            var rt = new ResourceType { Name = UniqueName("RT"), DefaultShiftCount = 2 };
            context.ResourceTypes.Add(rt);
            await context.SaveChangesAsync();

            var start = NorwegianToday().AddDays(daysFromToday).AddHours(8);
            var end = start.AddHours(8);
            var evt = new Event
            {
                Name = UniqueName("Event"),
                StartTime = start,
                EndTime = end,
                Resources = new List<EventResource>
                {
                    new EventResource { ResourceTypeId = rt.ResourceTypeId, StartTime = start, EndTime = end, ShiftCount = 1 },
                    new EventResource { ResourceTypeId = rt.ResourceTypeId, StartTime = start, EndTime = end, ShiftCount = 1 },
                },
            };
            context.Events.Add(evt);
            await context.SaveChangesAsync();
            return evt;
        }

        private static async Task SeedShift(PlannerDbContext context, EventResource resource, User user)
        {
            context.Shifts.Add(new EventResourceUser
            {
                EventResourceId = resource.EventResourceId,
                UserId = user.UserId,
                StartTime = resource.StartTime,
                EndTime = resource.EndTime,
            });
            await context.SaveChangesAsync();
        }

        [Fact]
        public async Task GetHallOfFame_SortsByShiftsDescendingThenByFirstName()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var events = new List<Event>();
            for (var i = 1; i <= 3; i++)
            {
                events.Add(await SeedEvent(seedContext, daysFromToday: -i));
            }
            var todayEvent = await SeedEvent(seedContext, daysFromToday: 0);
            var futureEvent = await SeedEvent(seedContext, daysFromToday: 7);

            // Fornavnene har felles prefiks så rekkefølgen mellom dem er entydig uavhengig av andre testers data.
            var prefix = Guid.NewGuid().ToString("N")[..8];
            var most = await SeedUser(seedContext, $"{prefix}_Carl");
            var tiedB = await SeedUser(seedContext, $"{prefix}_Bjarne");
            var tiedA = await SeedUser(seedContext, $"{prefix}_Anne");
            var least = await SeedUser(seedContext, $"{prefix}_Ada");
            var futureOnly = await SeedUser(seedContext, $"{prefix}_Fremtid");
            var todayOnly = await SeedUser(seedContext, $"{prefix}_IDag");

            // most: tre arrangementer, inkludert to vakter på samme arrangement (telles én gang).
            await SeedShift(seedContext, events[0].Resources.First(), most);
            await SeedShift(seedContext, events[0].Resources.Last(), most);
            await SeedShift(seedContext, events[1].Resources.First(), most);
            await SeedShift(seedContext, events[2].Resources.First(), most);

            // tiedA/tiedB: to arrangementer hver – skal sorteres på fornavn.
            await SeedShift(seedContext, events[0].Resources.First(), tiedB);
            await SeedShift(seedContext, events[1].Resources.First(), tiedB);
            await SeedShift(seedContext, events[1].Resources.First(), tiedA);
            await SeedShift(seedContext, events[2].Resources.First(), tiedA);

            // least: ett arrangement, selv om fornavnet sorteres først alfabetisk.
            await SeedShift(seedContext, events[2].Resources.First(), least);

            // futureOnly: kun fremtidige vakter, skal ikke være med.
            await SeedShift(seedContext, futureEvent.Resources.First(), futureOnly);

            // todayOnly: kun vakt i dag (norsk dato), skal ikke være med før dagen er omme.
            await SeedShift(seedContext, todayEvent.Resources.First(), todayOnly);

            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act
            var result = await service.GetHallOfFame();

            // Assert
            var all = result.HallOfFamers.ToList();
            for (var i = 1; i < all.Count; i++)
            {
                Assert.True(all[i - 1].Shifts >= all[i].Shifts,
                    $"Listen er ikke sortert synkende på antall vakter: {all[i - 1].Shifts} før {all[i].Shifts}");
            }

            var seededIds = new[] { most.UserId, tiedA.UserId, tiedB.UserId, least.UserId, futureOnly.UserId, todayOnly.UserId };
            var seeded = all.Where(h => seededIds.Contains(h.Id)).ToList();

            Assert.Equal(
                new[] { most.UserId, tiedA.UserId, tiedB.UserId, least.UserId },
                seeded.Select(h => h.Id));
            Assert.Equal(new[] { 3, 2, 2, 1 }, seeded.Select(h => h.Shifts));
            Assert.Equal($"{prefix}_Anne HallOfFame", seeded[1].FullName);
        }

        [Fact]
        public async Task GetUserById_ThrowsEntityNotFound_WhenNotFound()
        {
            // Arrange
            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act & Assert
            await Assert.ThrowsAsync<EntityNotFoundException>(() => service.GetUserById(int.MaxValue));
        }

        [Fact]
        public async Task Update_ThrowsEntityNotFound_WhenNotFound()
        {
            // Arrange
            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act & Assert
            await Assert.ThrowsAsync<EntityNotFoundException>(() => service.Update(int.MaxValue, new UserRequest { FirstName = "X" }));
        }

        private static string UniquePhoneNo() => Random.Shared.Next(40000000, 99999999).ToString();

        private PlannerDbContext CreateContextWithBeforeSave(Func<Task> beforeSave)
            => _fixture.CreateContext(new BeforeSaveInterceptor(beforeSave));

        private async Task SeedUserInOtherContext(string phoneNo)
        {
            using var otherContext = _fixture.CreateContext();
            await SeedUserWithPhone(otherContext, phoneNo);
        }

        private static async Task<User> SeedUserWithPhone(PlannerDbContext context, string phoneNo, bool isAdmin = false)
        {
            var user = new User
            {
                UserName = phoneNo,
                FirstName = "Opprinnelig",
                LastName = "Bruker",
                IsAdmin = isAdmin,
                Created = DateTime.UtcNow,
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();
            return user;
        }

        [Fact]
        public void UpdateMeRequest_HasNoAdminOrPhoneNoField()
        {
            var properties = typeof(UpdateMeRequest).GetProperties().Select(p => p.Name).ToList();

            Assert.DoesNotContain(nameof(UserRequest.IsAdmin), properties);
            Assert.DoesNotContain(nameof(UserRequest.PhoneNo), properties);
        }

        [Fact]
        public async Task UpdateMe_SetsIsHidden_AndLeavesItUnchangedWhenNull()
        {
            using var seedContext = _fixture.CreateContext();
            var user = await SeedUserWithPhone(seedContext, UniquePhoneNo());

            using (var context = _fixture.CreateContext())
            {
                var result = await CreateService(context).UpdateMe(user.UserId, new UpdateMeRequest { IsHidden = true });
                Assert.True(result.IsHidden);
                Assert.False(result.IsAdmin);
            }

            using (var context = _fixture.CreateContext())
            {
                var result = await CreateService(context).UpdateMe(user.UserId, new UpdateMeRequest { FirstName = "Fortsatt skjult" });
                Assert.True(result.IsHidden);
            }

            using var verifyContext = _fixture.CreateContext();
            Assert.True(verifyContext.Users.Single(u => u.UserId == user.UserId).IsHidden);
        }

        [Fact]
        public async Task UpdateMe_UpdatesName_AndKeepsUserName()
        {
            using var seedContext = _fixture.CreateContext();
            var phoneNo = UniquePhoneNo();
            var user = await SeedUserWithPhone(seedContext, phoneNo);

            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            var result = await service.UpdateMe(user.UserId, new UpdateMeRequest
            {
                FirstName = "Nytt",
                LastName = "Navn",
            });

            Assert.Equal("Nytt", result.FirstName);
            Assert.Equal("Navn", result.LastName);
            Assert.Equal(phoneNo, result.PhoneNo);
            Assert.False(result.IsAdmin);

            using var verifyContext = _fixture.CreateContext();
            var stored = verifyContext.Users.Single(u => u.UserId == user.UserId);
            Assert.Equal(phoneNo, stored.UserName);
            Assert.False(stored.IsAdmin);
            Assert.False(stored.IsHidden);
        }

        [Fact]
        public async Task UpdateMe_KeepsExistingAdmin()
        {
            using var seedContext = _fixture.CreateContext();
            var admin = await SeedUserWithPhone(seedContext, UniquePhoneNo(), isAdmin: true);

            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            var result = await service.UpdateMe(admin.UserId, new UpdateMeRequest { FirstName = "Admin" });

            Assert.True(result.IsAdmin);
            using var verifyContext = _fixture.CreateContext();
            Assert.True(verifyContext.Users.Single(u => u.UserId == admin.UserId).IsAdmin);
        }

        [Fact]
        public async Task UpdateMe_ReturnsTrainings()
        {
            using var seedContext = _fixture.CreateContext();
            var user = await SeedUserWithPhone(seedContext, UniquePhoneNo());
            var rt = new ResourceType { Name = UniqueName("RT"), DefaultShiftCount = 1 };
            seedContext.ResourceTypes.Add(rt);
            await seedContext.SaveChangesAsync();
            seedContext.ResourceTypeTrainings.Add(new ResourceTypeTraining
            {
                UserId = user.UserId,
                ResourceTypeId = rt.ResourceTypeId,
                TrainingComplete = true,
            });
            await seedContext.SaveChangesAsync();

            using var context = _fixture.CreateContext();
            var result = await CreateService(context).UpdateMe(user.UserId, new UpdateMeRequest { FirstName = "Med opplæring" });

            var training = Assert.Single(result.Trainings);
            Assert.Equal(rt.ResourceTypeId, training.ResourceTypeId);
            Assert.Equal(rt.Name, training.ResourceTypeName);
            Assert.True(training.TrainingComplete);
        }

        [Fact]
        public async Task Update_ThrowsDomainValidation_WhenPhoneNoBelongsToAnotherUser()
        {
            using var seedContext = _fixture.CreateContext();
            var user = await SeedUserWithPhone(seedContext, UniquePhoneNo());
            var otherPhoneNo = UniquePhoneNo();
            await SeedUserWithPhone(seedContext, otherPhoneNo);

            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            var ex = await Assert.ThrowsAsync<DomainValidationException>(
                () => service.Update(user.UserId, new UserRequest { PhoneNo = $"0047{otherPhoneNo}" }));
            Assert.Equal(UserService.PhoneNoInUseMessage, ex.Message);
        }

        [Fact]
        public async Task Update_ChangesPhoneNo_AndStoresItNormalized()
        {
            using var seedContext = _fixture.CreateContext();
            var user = await SeedUserWithPhone(seedContext, UniquePhoneNo());
            var newPhoneNo = UniquePhoneNo();

            using var context = _fixture.CreateContext();
            var result = await CreateService(context).Update(user.UserId, new UserRequest { PhoneNo = $"+47 {newPhoneNo}" });

            Assert.Equal(newPhoneNo, result.PhoneNo);
            using var verifyContext = _fixture.CreateContext();
            Assert.Equal(newPhoneNo, verifyContext.Users.Single(u => u.UserId == user.UserId).UserName);
        }

        [Fact]
        public async Task Update_AllowsKeepingOwnPhoneNo()
        {
            using var seedContext = _fixture.CreateContext();
            var phoneNo = UniquePhoneNo();
            var user = await SeedUserWithPhone(seedContext, phoneNo);

            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            var result = await service.Update(user.UserId, new UserRequest { PhoneNo = phoneNo, IsAdmin = true });

            Assert.Equal(phoneNo, result.PhoneNo);
            Assert.True(result.IsAdmin);
        }

        [Fact]
        public async Task Update_AllowsSamePhoneNoInOtherFormat()
        {
            using var seedContext = _fixture.CreateContext();
            var phoneNo = UniquePhoneNo();
            var user = await SeedUserWithPhone(seedContext, phoneNo);

            using var context = _fixture.CreateContext();
            var result = await CreateService(context).Update(user.UserId, new UserRequest { FirstName = "Endret", PhoneNo = $"+47 {phoneNo}" });

            Assert.Equal("Endret", result.FirstName);
            Assert.Equal(phoneNo, result.PhoneNo);
        }

        [Fact]
        public async Task Update_ThrowsDomainValidation_WhenPhoneNoIsTakenConcurrently()
        {
            using var seedContext = _fixture.CreateContext();
            var user = await SeedUserWithPhone(seedContext, UniquePhoneNo());
            var newPhoneNo = UniquePhoneNo();

            // En parallell forespørsel tar nummeret mellom sjekken og lagringen.
            using var context = CreateContextWithBeforeSave(() => SeedUserInOtherContext(newPhoneNo));
            var ex = await Assert.ThrowsAsync<DomainValidationException>(
                () => CreateService(context).Update(user.UserId, new UserRequest { PhoneNo = newPhoneNo }));
            Assert.Equal(UserService.PhoneNoInUseMessage, ex.Message);
        }

        [Fact]
        public async Task Update_AllowsUnchangedUserName_WhenItIsNotAPhoneNo()
        {
            using var seedContext = _fixture.CreateContext();
            var user = await SeedUserWithPhone(seedContext, "admin");

            using var context = _fixture.CreateContext();
            var result = await CreateService(context).Update(user.UserId, new UserRequest { FirstName = "Admin", PhoneNo = "admin", IsAdmin = true });

            Assert.Equal("admin", result.PhoneNo);
            Assert.True(result.IsAdmin);
            using var verifyContext = _fixture.CreateContext();
            Assert.Equal("admin", verifyContext.Users.Single(u => u.UserId == user.UserId).UserName);
        }

        [Fact]
        public async Task Create_ThrowsDomainValidation_WhenPhoneNoIsInUse()
        {
            using var seedContext = _fixture.CreateContext();
            var existing = await SeedUserWithPhone(seedContext, UniquePhoneNo());

            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            var ex = await Assert.ThrowsAsync<DomainValidationException>(
                () => service.Create(new UserRequest { FirstName = "Ny", PhoneNo = $"+47 {existing.UserName}" }));
            Assert.Equal(UserService.PhoneNoInUseMessage, ex.Message);
        }

        [Fact]
        public async Task Create_ReactivatesInactiveUser_WithSamePhoneNo()
        {
            using var seedContext = _fixture.CreateContext();
            var phoneNo = UniquePhoneNo();
            var deleted = await SeedUserWithPhone(seedContext, phoneNo);
            deleted.Inactive = true;
            await seedContext.SaveChangesAsync();

            using var context = _fixture.CreateContext();
            var result = await CreateService(context).Create(new UserRequest
            {
                FirstName = "Tilbake",
                LastName = "Igjen",
                PhoneNo = $"+47 {phoneNo}",
                IsAdmin = true,
                IsHidden = true,
                Password = "hemmelig",
            });

            Assert.Equal(deleted.UserId, result.Id);
            Assert.Equal("Tilbake", result.FirstName);
            Assert.Equal("Igjen", result.LastName);
            Assert.Equal(phoneNo, result.PhoneNo);
            Assert.True(result.IsAdmin);
            Assert.True(result.IsHidden);

            using var verifyContext = _fixture.CreateContext();
            var stored = verifyContext.Users.Single(u => u.UserId == deleted.UserId);
            Assert.False(stored.Inactive);
            Assert.Equal(phoneNo, stored.UserName);
            Assert.NotNull(stored.Salt);
            Assert.True(PasswordHasher.VerifyHash("hemmelig", stored.Salt!, stored.EncryptedPassword!));
            Assert.Single(verifyContext.Users.Where(u => u.UserName == phoneNo));
        }

        [Fact]
        public async Task Create_ThrowsDomainValidation_WhenPhoneNoIsTakenConcurrently()
        {
            var phoneNo = UniquePhoneNo();

            // En parallell forespørsel oppretter brukeren mellom sjekken og lagringen.
            using var context = CreateContextWithBeforeSave(() => SeedUserInOtherContext(phoneNo));
            var ex = await Assert.ThrowsAsync<DomainValidationException>(
                () => CreateService(context).Create(new UserRequest { FirstName = "Ny", PhoneNo = phoneNo }));
            Assert.Equal(UserService.PhoneNoInUseMessage, ex.Message);

            using var verifyContext = _fixture.CreateContext();
            Assert.Single(verifyContext.Users.Where(u => u.UserName == phoneNo));
        }

        [Fact]
        public async Task Create_StoresNormalizedPhoneNo()
        {
            using var context = _fixture.CreateContext();
            var service = CreateService(context);
            var phoneNo = UniquePhoneNo();

            var result = await service.Create(new UserRequest { FirstName = "Ny", PhoneNo = $"+47 {phoneNo[..3]} {phoneNo[3..]}" });

            Assert.Equal(phoneNo, result.PhoneNo);
        }

        [Fact]
        public async Task Create_ThrowsDomainValidation_WhenPhoneNoIsInvalid()
        {
            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            var ex = await Assert.ThrowsAsync<DomainValidationException>(
                () => service.Create(new UserRequest { FirstName = "Ny", PhoneNo = "123" }));
            Assert.Equal(UserService.PhoneNoInvalidMessage, ex.Message);
        }

        [Theory]
        [InlineData("46{0}")]
        [InlineData("+46 {0}")]
        [InlineData("1{0}")]
        public async Task Create_ThrowsDomainValidation_ForForeignPhoneNo_EvenWhenNorwegianUserWithSameDigitsExists(string format)
        {
            using var seedContext = _fixture.CreateContext();
            var phoneNo = UniquePhoneNo();
            await SeedUserWithPhone(seedContext, phoneNo);

            using var context = _fixture.CreateContext();
            var ex = await Assert.ThrowsAsync<DomainValidationException>(
                () => CreateService(context).Create(new UserRequest { FirstName = "Ny", PhoneNo = string.Format(format, phoneNo) }));
            Assert.Equal(UserService.PhoneNoInvalidMessage, ex.Message);
        }

        [Fact]
        public async Task Update_ThrowsDomainValidation_ForForeignPhoneNo()
        {
            using var seedContext = _fixture.CreateContext();
            var phoneNo = UniquePhoneNo();
            var user = await SeedUserWithPhone(seedContext, phoneNo);

            using var context = _fixture.CreateContext();
            var ex = await Assert.ThrowsAsync<DomainValidationException>(
                () => CreateService(context).Update(user.UserId, new UserRequest { PhoneNo = $"+46 {phoneNo}" }));
            Assert.Equal(UserService.PhoneNoInvalidMessage, ex.Message);

            using var verifyContext = _fixture.CreateContext();
            Assert.Equal(phoneNo, verifyContext.Users.Single(u => u.UserId == user.UserId).UserName);
        }

        [Fact]
        public async Task Update_ChangesInvalidUserName_ToValidPhoneNo()
        {
            using var seedContext = _fixture.CreateContext();
            var user = await SeedUserWithPhone(seedContext, $"admin_{Guid.NewGuid():N}");
            var phoneNo = UniquePhoneNo();

            using var context = _fixture.CreateContext();
            var result = await CreateService(context).Update(user.UserId, new UserRequest { PhoneNo = $"+47 {phoneNo}" });

            Assert.Equal(phoneNo, result.PhoneNo);
        }

        [Fact]
        public async Task Delete_ThrowsEntityNotFound_WhenNotFound()
        {
            // Arrange
            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act & Assert
            await Assert.ThrowsAsync<EntityNotFoundException>(() => service.Delete(int.MaxValue));
        }

        private static async Task<Guid> SeedSession(PlannerDbContext context, User user)
        {
            var session = new UserSession { UserId = user.UserId, AuthType = AuthType.Otp, Created = DateTime.UtcNow };
            context.UserSessions.Add(session);
            await context.SaveChangesAsync();
            return session.UserSessionId;
        }

        [Fact]
        public async Task Delete_DeactivatesUser_AndDeletesOnlyThatUsersSessions()
        {
            using var seedContext = _fixture.CreateContext();
            var user = await SeedUserWithPhone(seedContext, UniquePhoneNo());
            var other = await SeedUserWithPhone(seedContext, UniquePhoneNo());
            await SeedSession(seedContext, user);
            await SeedSession(seedContext, user);
            var otherSession = await SeedSession(seedContext, other);

            using var context = _fixture.CreateContext();
            await CreateService(context).Delete(user.UserId);

            using var verifyContext = _fixture.CreateContext();
            Assert.True(verifyContext.Users.Single(u => u.UserId == user.UserId).Inactive);
            Assert.False(verifyContext.UserSessions.Any(s => s.UserId == user.UserId));
            Assert.True(verifyContext.UserSessions.Any(s => s.UserSessionId == otherSession));
        }

        [Fact]
        public async Task Create_ReactivatesUser_AfterDelete()
        {
            using var seedContext = _fixture.CreateContext();
            var phoneNo = UniquePhoneNo();
            var user = await SeedUserWithPhone(seedContext, phoneNo);
            await SeedSession(seedContext, user);

            using (var deleteContext = _fixture.CreateContext())
                await CreateService(deleteContext).Delete(user.UserId);

            using var context = _fixture.CreateContext();
            var result = await CreateService(context).Create(new UserRequest { PhoneNo = phoneNo });

            Assert.Equal(user.UserId, result.Id);
            using var verifyContext = _fixture.CreateContext();
            Assert.False(verifyContext.Users.Single(u => u.UserId == user.UserId).Inactive);
        }
    }
}
