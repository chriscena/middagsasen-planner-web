using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services;
using Middagsasen.Planner.Api.Services.SmsSender;
using Middagsasen.Planner.Api.Services.Users;
using Middagsasen.Planner.Api.Tests.Infrastructure;
using NSubstitute;

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
            => new UserService(context, Substitute.For<ISmsSender>(), Substitute.For<IAuthSettings>());

        private static string UniqueName(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

        private static async Task<User> SeedUser(PlannerDbContext context, string firstName)
        {
            var user = new User
            {
                UserName = $"+47{Random.Shared.Next(10000000, 99999999)}",
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
            var rt = new ResourceType { Name = UniqueName("RT"), DefaultStaff = 2 };
            context.ResourceTypes.Add(rt);
            await context.SaveChangesAsync();

            var start = DateTime.Today.AddDays(daysFromToday).AddHours(8);
            var end = start.AddHours(8);
            var evt = new Event
            {
                Name = UniqueName("Event"),
                StartTime = start,
                EndTime = end,
                Resources = new List<EventResource>
                {
                    new EventResource { ResourceTypeId = rt.ResourceTypeId, StartTime = start, EndTime = end, MinimumStaff = 1 },
                    new EventResource { ResourceTypeId = rt.ResourceTypeId, StartTime = start, EndTime = end, MinimumStaff = 1 },
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
            var futureEvent = await SeedEvent(seedContext, daysFromToday: 7);

            // Fornavnene har felles prefiks så rekkefølgen mellom dem er entydig uavhengig av andre testers data.
            var prefix = Guid.NewGuid().ToString("N")[..8];
            var most = await SeedUser(seedContext, $"{prefix}_Carl");
            var tiedB = await SeedUser(seedContext, $"{prefix}_Bjarne");
            var tiedA = await SeedUser(seedContext, $"{prefix}_Anne");
            var least = await SeedUser(seedContext, $"{prefix}_Ada");
            var futureOnly = await SeedUser(seedContext, $"{prefix}_Fremtid");

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

            var seededIds = new[] { most.UserId, tiedA.UserId, tiedB.UserId, least.UserId, futureOnly.UserId };
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

        [Fact]
        public async Task Delete_ThrowsEntityNotFound_WhenNotFound()
        {
            // Arrange
            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act & Assert
            await Assert.ThrowsAsync<EntityNotFoundException>(() => service.Delete(int.MaxValue));
        }
    }
}
