using Microsoft.EntityFrameworkCore;
using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services;
using Middagsasen.Planner.Api.Services.Events;
using Middagsasen.Planner.Api.Services.ResourceTypes;
using Middagsasen.Planner.Api.Tests.Infrastructure;
using NSubstitute;

namespace Middagsasen.Planner.Api.Tests.Services.Events
{
    [Collection("Database")]
    public class EventsServiceIntegrationTests
    {
        private readonly DatabaseFixture _fixture;
        private readonly IResourceTypesService _resourceTypesService;

        public EventsServiceIntegrationTests(DatabaseFixture fixture)
        {
            _fixture = fixture;
            _resourceTypesService = Substitute.For<IResourceTypesService>();
        }

        private static ICurrentUserService MockCurrentUser(int userId, bool isAdmin = false)
        {
            var mock = Substitute.For<ICurrentUserService>();
            mock.UserId.Returns(userId);
            mock.IsAdmin.Returns(isAdmin);
            return mock;
        }

        private EventsService CreateService(PlannerDbContext context, int userId = 0, bool isAdmin = false)
        {
            var currentUser = MockCurrentUser(userId, isAdmin);
            return new EventsService(context, _resourceTypesService, currentUser);
        }

        private static string UniqueName(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

        private async Task<User> SeedUser(PlannerDbContext context, string? firstName = null, bool isAdmin = false)
        {
            var user = new User
            {
                UserName = $"+47{Random.Shared.Next(10000000, 99999999)}",
                FirstName = firstName ?? "Test",
                LastName = "User",
                Created = DateTime.UtcNow,
                IsAdmin = isAdmin,
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();
            return user;
        }

        private async Task<ResourceType> SeedResourceType(PlannerDbContext context, string? name = null)
        {
            var rt = new ResourceType
            {
                Name = name ?? UniqueName("RT"),
                DefaultStaff = 2,
            };
            context.ResourceTypes.Add(rt);
            await context.SaveChangesAsync();
            return rt;
        }

        private async Task<(Event evt, EventResource resource)> SeedEventWithResource(PlannerDbContext context)
        {
            var rt = await SeedResourceType(context);
            var evt = new Event
            {
                Name = UniqueName("Event"),
                StartTime = new DateTime(2026, 1, 15, 8, 0, 0),
                EndTime = new DateTime(2026, 1, 15, 16, 0, 0),
                Resources = new List<EventResource>
                {
                    new EventResource
                    {
                        ResourceTypeId = rt.ResourceTypeId,
                        StartTime = new DateTime(2026, 1, 15, 8, 0, 0),
                        EndTime = new DateTime(2026, 1, 15, 16, 0, 0),
                        MinimumStaff = 2,
                    }
                }
            };
            context.Events.Add(evt);
            await context.SaveChangesAsync();
            return (evt, evt.Resources.First());
        }

        #region Event CRUD

        [Fact]
        public async Task CreateEvent_PersistsToDatabase()
        {
            // Arrange
            var name = UniqueName("Create");
            using var seedContext = _fixture.CreateContext();
            var rt = await SeedResourceType(seedContext);

            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            var request = new EventRequest
            {
                Name = name,
                Description = "Test description",
                StartTime = "2026-01-15T08:00:00",
                EndTime = "2026-01-15T16:00:00",
                Resources = new List<ResourceRequest>
                {
                    new ResourceRequest
                    {
                        ResourceTypeId = rt.ResourceTypeId,
                        StartTime = "2026-01-15T09:00:00",
                        EndTime = "2026-01-15T15:00:00",
                        MinimumStaff = 3,
                    }
                }
            };

            // Act
            var result = await service.CreateEvent(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(name, result.Name);
            Assert.Equal("Test description", result.Description);

            // Verify in DB with fresh context
            using var verifyContext = _fixture.CreateContext();
            var dbEvent = await verifyContext.Events
                .Include(e => e.Resources)
                .AsNoTracking()
                .SingleOrDefaultAsync(e => e.EventId == result.Id);
            Assert.NotNull(dbEvent);
            Assert.Equal(name, dbEvent.Name);
            Assert.Single(dbEvent.Resources);
            Assert.Equal(3, dbEvent.Resources.First().MinimumStaff);
        }

        [Fact]
        public async Task GetEventById_ReturnsEventWithResources()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var (evt, resource) = await SeedEventWithResource(seedContext);

            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act
            var result = await service.GetEventById(evt.EventId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(evt.EventId, result.Id);
            Assert.Equal(evt.Name, result.Name);
            Assert.Single(result.Resources);
            Assert.Equal(resource.EventResourceId, result.Resources.First().Id);
            Assert.Equal(2, result.Resources.First().MinimumStaff);
        }

        [Fact]
        public async Task GetEvents_FiltersByDateRange()
        {
            // Arrange — seed events at different dates with unique names
            var janName = UniqueName("Jan");
            var febName = UniqueName("Feb");

            using var seedContext = _fixture.CreateContext();
            var rt = await SeedResourceType(seedContext);

            seedContext.Events.Add(new Event
            {
                Name = janName,
                StartTime = new DateTime(2027, 1, 10, 8, 0, 0),
                EndTime = new DateTime(2027, 1, 10, 16, 0, 0),
                Resources = new List<EventResource>
                {
                    new EventResource { ResourceTypeId = rt.ResourceTypeId, StartTime = new DateTime(2027, 1, 10, 8, 0, 0), EndTime = new DateTime(2027, 1, 10, 16, 0, 0), MinimumStaff = 1 }
                }
            });
            seedContext.Events.Add(new Event
            {
                Name = febName,
                StartTime = new DateTime(2027, 2, 10, 8, 0, 0),
                EndTime = new DateTime(2027, 2, 10, 16, 0, 0),
                Resources = new List<EventResource>
                {
                    new EventResource { ResourceTypeId = rt.ResourceTypeId, StartTime = new DateTime(2027, 2, 10, 8, 0, 0), EndTime = new DateTime(2027, 2, 10, 16, 0, 0), MinimumStaff = 1 }
                }
            });
            await seedContext.SaveChangesAsync();

            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act — query only January 2027
            var results = (await service.GetEvents(new DateTime(2027, 1, 1), new DateTime(2027, 2, 1))).ToList();

            // Assert
            Assert.Contains(results, r => r != null && r.Name == janName);
            Assert.DoesNotContain(results, r => r != null && r.Name == febName);
        }

        [Fact]
        public async Task UpdateEvent_UpdatesNameAndDescription()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var (evt, resource) = await SeedEventWithResource(seedContext);

            var updatedName = UniqueName("Updated");
            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            var request = new EventRequest
            {
                Name = updatedName,
                Description = "Updated description",
                StartTime = "2026-01-15T08:00:00",
                EndTime = "2026-01-15T16:00:00",
                Resources = new List<ResourceRequest>
                {
                    new ResourceRequest
                    {
                        Id = resource.EventResourceId,
                        ResourceTypeId = resource.ResourceTypeId,
                        StartTime = "2026-01-15T08:00:00",
                        EndTime = "2026-01-15T16:00:00",
                        MinimumStaff = resource.MinimumStaff,
                    }
                }
            };

            // Act
            var result = await service.UpdateEvent(evt.EventId, request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(updatedName, result.Name);
            Assert.Equal("Updated description", result.Description);

            // Verify in DB
            using var verifyContext = _fixture.CreateContext();
            var dbEvent = await verifyContext.Events
                .AsNoTracking()
                .SingleAsync(e => e.EventId == evt.EventId);
            Assert.Equal(updatedName, dbEvent.Name);
            Assert.Equal("Updated description", dbEvent.Description);
        }

        [Fact]
        public async Task UpdateEvent_CanAddAndRemoveResources()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var (evt, existingResource) = await SeedEventWithResource(seedContext);
            var newRt = await SeedResourceType(seedContext, UniqueName("NewRT"));

            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            var request = new EventRequest
            {
                Name = evt.Name,
                StartTime = "2026-01-15T08:00:00",
                EndTime = "2026-01-15T16:00:00",
                Resources = new List<ResourceRequest>
                {
                    // Delete existing resource
                    new ResourceRequest
                    {
                        Id = existingResource.EventResourceId,
                        ResourceTypeId = existingResource.ResourceTypeId,
                        StartTime = "2026-01-15T08:00:00",
                        EndTime = "2026-01-15T16:00:00",
                        MinimumStaff = existingResource.MinimumStaff,
                        IsDeleted = true,
                    },
                    // Add new resource
                    new ResourceRequest
                    {
                        ResourceTypeId = newRt.ResourceTypeId,
                        StartTime = "2026-01-15T10:00:00",
                        EndTime = "2026-01-15T14:00:00",
                        MinimumStaff = 4,
                    }
                }
            };

            // Act
            var result = await service.UpdateEvent(evt.EventId, request);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result.Resources);
            var newResource = result.Resources.First();
            Assert.Equal(newRt.ResourceTypeId, newResource.ResourceType.Id);
            Assert.Equal(4, newResource.MinimumStaff);

            // Verify old resource is gone
            using var verifyContext = _fixture.CreateContext();
            var oldResource = await verifyContext.EventResource
                .AsNoTracking()
                .SingleOrDefaultAsync(r => r.EventResourceId == existingResource.EventResourceId);
            Assert.Null(oldResource);
        }

        [Fact]
        public async Task DeleteEvent_RemovesFromDatabase()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var (evt, _) = await SeedEventWithResource(seedContext);

            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act
            var result = await service.DeleteEvent(evt.EventId);

            // Assert
            Assert.NotNull(result);

            using var verifyContext = _fixture.CreateContext();
            var dbEvent = await verifyContext.Events
                .AsNoTracking()
                .SingleOrDefaultAsync(e => e.EventId == evt.EventId);
            Assert.Null(dbEvent);
        }

        [Fact]
        public async Task DeleteEvent_ReturnsNull_WhenNotFound()
        {
            // Arrange
            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act
            var result = await service.DeleteEvent(999999);

            // Assert
            Assert.Null(result);
        }

        #endregion

        #region Shifts

        [Fact]
        public async Task AddShift_PersistsShiftToDatabase()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var user = await SeedUser(seedContext);
            var (evt, resource) = await SeedEventWithResource(seedContext);

            using var context = _fixture.CreateContext();
            var service = CreateService(context, userId: user.UserId, isAdmin: false);

            var request = new ShiftRequest
            {
                UserId = user.UserId,
                StartTime = new DateTime(2026, 1, 15, 9, 0, 0),
                EndTime = new DateTime(2026, 1, 15, 15, 0, 0),
                Comment = "Test shift",
            };

            // Act
            var result = await service.AddShift(resource.EventResourceId, request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(user.UserId, result.User.Id);
            Assert.Equal("Test shift", result.Comment);

            // Verify in DB
            using var verifyContext = _fixture.CreateContext();
            var dbShift = await verifyContext.Shifts
                .AsNoTracking()
                .SingleOrDefaultAsync(s => s.EventResourceUserId == result.Id);
            Assert.NotNull(dbShift);
            Assert.Equal(user.UserId, dbShift.UserId);
            Assert.Equal(resource.EventResourceId, dbShift.EventResourceId);
        }

        [Fact]
        public async Task AddShift_ThrowsForbiddenAccessException_WhenNonAdminAddsForOtherUser()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var currentUser = await SeedUser(seedContext, "Current");
            var otherUser = await SeedUser(seedContext, "Other");
            var (evt, resource) = await SeedEventWithResource(seedContext);

            using var context = _fixture.CreateContext();
            var service = CreateService(context, userId: currentUser.UserId, isAdmin: false);

            var request = new ShiftRequest
            {
                UserId = otherUser.UserId,
                StartTime = new DateTime(2026, 1, 15, 9, 0, 0),
                EndTime = new DateTime(2026, 1, 15, 15, 0, 0),
            };

            // Act & Assert
            await Assert.ThrowsAsync<ForbiddenAccessException>(
                () => service.AddShift(resource.EventResourceId, request));
        }

        [Fact]
        public async Task AddShift_AllowsAdmin_ToAddForOtherUser()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var adminUser = await SeedUser(seedContext, "Admin", isAdmin: true);
            var otherUser = await SeedUser(seedContext, "Other");
            var (evt, resource) = await SeedEventWithResource(seedContext);

            using var context = _fixture.CreateContext();
            var service = CreateService(context, userId: adminUser.UserId, isAdmin: true);

            var request = new ShiftRequest
            {
                UserId = otherUser.UserId,
                StartTime = new DateTime(2026, 1, 15, 9, 0, 0),
                EndTime = new DateTime(2026, 1, 15, 15, 0, 0),
            };

            // Act
            var result = await service.AddShift(resource.EventResourceId, request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(otherUser.UserId, result.User.Id);
        }

        [Fact]
        public async Task UpdateShift_UpdatesComment()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var user = await SeedUser(seedContext);
            var (evt, resource) = await SeedEventWithResource(seedContext);

            var shift = new EventResourceUser
            {
                EventResourceId = resource.EventResourceId,
                UserId = user.UserId,
                StartTime = new DateTime(2026, 1, 15, 9, 0, 0),
                EndTime = new DateTime(2026, 1, 15, 15, 0, 0),
                Comment = "Original",
            };
            seedContext.Shifts.Add(shift);
            await seedContext.SaveChangesAsync();

            using var context = _fixture.CreateContext();
            var service = CreateService(context, userId: user.UserId, isAdmin: false);

            var request = new ShiftRequest
            {
                UserId = user.UserId,
                StartTime = new DateTime(2026, 1, 15, 9, 0, 0),
                EndTime = new DateTime(2026, 1, 15, 15, 0, 0),
                Comment = "Updated comment",
            };

            // Act
            var result = await service.UpdateShift(shift.EventResourceUserId, request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Updated comment", result.Comment);

            // Verify in DB
            using var verifyContext = _fixture.CreateContext();
            var dbShift = await verifyContext.Shifts
                .AsNoTracking()
                .SingleAsync(s => s.EventResourceUserId == shift.EventResourceUserId);
            Assert.Equal("Updated comment", dbShift.Comment);
        }

        [Fact]
        public async Task UpdateShift_DefaultsUserIdToCurrentUser_WhenZero()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var user = await SeedUser(seedContext);
            var (evt, resource) = await SeedEventWithResource(seedContext);

            var shift = new EventResourceUser
            {
                EventResourceId = resource.EventResourceId,
                UserId = user.UserId,
                StartTime = new DateTime(2026, 1, 15, 9, 0, 0),
                EndTime = new DateTime(2026, 1, 15, 15, 0, 0),
            };
            seedContext.Shifts.Add(shift);
            await seedContext.SaveChangesAsync();

            using var context = _fixture.CreateContext();
            var service = CreateService(context, userId: user.UserId, isAdmin: false);

            var request = new ShiftRequest
            {
                UserId = 0, // Should default to currentUserId
                Comment = "Zero user test",
            };

            // Act
            var result = await service.UpdateShift(shift.EventResourceUserId, request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(user.UserId, result.User.Id);

            // Verify in DB
            using var verifyContext = _fixture.CreateContext();
            var dbShift = await verifyContext.Shifts
                .AsNoTracking()
                .SingleAsync(s => s.EventResourceUserId == shift.EventResourceUserId);
            Assert.Equal(user.UserId, dbShift.UserId);
        }

        [Fact]
        public async Task DeleteShift_RemovesFromDatabase()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var user = await SeedUser(seedContext);
            var (evt, resource) = await SeedEventWithResource(seedContext);

            var shift = new EventResourceUser
            {
                EventResourceId = resource.EventResourceId,
                UserId = user.UserId,
                StartTime = new DateTime(2026, 1, 15, 9, 0, 0),
                EndTime = new DateTime(2026, 1, 15, 15, 0, 0),
            };
            seedContext.Shifts.Add(shift);
            await seedContext.SaveChangesAsync();

            using var context = _fixture.CreateContext();
            var service = CreateService(context, userId: user.UserId, isAdmin: false);

            // Act
            var result = await service.DeleteShift(shift.EventResourceUserId);

            // Assert
            Assert.NotNull(result);

            using var verifyContext = _fixture.CreateContext();
            var dbShift = await verifyContext.Shifts
                .AsNoTracking()
                .SingleOrDefaultAsync(s => s.EventResourceUserId == shift.EventResourceUserId);
            Assert.Null(dbShift);
        }

        [Fact]
        public async Task DeleteShift_ThrowsForbiddenAccessException_WhenNonAdminDeletesOtherUsersShift()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var shiftOwner = await SeedUser(seedContext, "Owner");
            var otherUser = await SeedUser(seedContext, "Other");
            var (evt, resource) = await SeedEventWithResource(seedContext);

            var shift = new EventResourceUser
            {
                EventResourceId = resource.EventResourceId,
                UserId = shiftOwner.UserId,
                StartTime = new DateTime(2026, 1, 15, 9, 0, 0),
                EndTime = new DateTime(2026, 1, 15, 15, 0, 0),
            };
            seedContext.Shifts.Add(shift);
            await seedContext.SaveChangesAsync();

            using var context = _fixture.CreateContext();
            var service = CreateService(context, userId: otherUser.UserId, isAdmin: false);

            // Act & Assert
            await Assert.ThrowsAsync<ForbiddenAccessException>(
                () => service.DeleteShift(shift.EventResourceUserId));
        }

        #endregion

        #region Messages

        [Fact]
        public async Task AddMessage_PersistsMessageToDatabase()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var user = await SeedUser(seedContext);
            var (evt, resource) = await SeedEventWithResource(seedContext);

            using var context = _fixture.CreateContext();
            var service = CreateService(context, userId: user.UserId);

            var request = new MessageRequest
            {
                EventResourceId = resource.EventResourceId,
                Message = "Test message",
            };

            // Act
            var result = await service.AddMessage(resource.EventResourceId, request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Test message", result.Message);
            Assert.Equal(resource.EventResourceId, result.EventResourceId);

            // Verify in DB
            using var verifyContext = _fixture.CreateContext();
            var dbMessage = await verifyContext.Messages
                .AsNoTracking()
                .SingleOrDefaultAsync(m => m.EventResourceMessageId == result.Id);
            Assert.NotNull(dbMessage);
            Assert.Equal("Test message", dbMessage.Message);
        }

        [Fact]
        public async Task AddMessage_SetsCreatedByToProvidedUserId()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var user = await SeedUser(seedContext);
            var (evt, resource) = await SeedEventWithResource(seedContext);

            using var context = _fixture.CreateContext();
            var service = CreateService(context, userId: user.UserId);

            var request = new MessageRequest
            {
                EventResourceId = resource.EventResourceId,
                Message = "CreatedBy test",
            };

            // Act
            var result = await service.AddMessage(resource.EventResourceId, request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(user.UserId, result.CreatedBy.Id);

            // Verify in DB
            using var verifyContext = _fixture.CreateContext();
            var dbMessage = await verifyContext.Messages
                .AsNoTracking()
                .SingleAsync(m => m.EventResourceMessageId == result.Id);
            Assert.Equal(user.UserId, dbMessage.CreatedBy);
        }

        [Fact]
        public async Task DeleteMessage_RemovesFromDatabase()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var user = await SeedUser(seedContext);
            var (evt, resource) = await SeedEventWithResource(seedContext);

            var message = new EventResourceMessage
            {
                EventResourceId = resource.EventResourceId,
                Message = "To be deleted",
                Created = DateTime.UtcNow,
                CreatedBy = user.UserId,
            };
            seedContext.Messages.Add(message);
            await seedContext.SaveChangesAsync();

            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act
            var result = await service.DeleteMessage(message.EventResourceMessageId, resource.EventResourceId);

            // Assert
            Assert.NotNull(result);

            using var verifyContext = _fixture.CreateContext();
            var dbMessage = await verifyContext.Messages
                .AsNoTracking()
                .SingleOrDefaultAsync(m => m.EventResourceMessageId == message.EventResourceMessageId);
            Assert.Null(dbMessage);
        }

        #endregion

        #region MinimumStaff

        [Fact]
        public async Task UpdateMinimumStaff_UpdatesValue()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var (evt, resource) = await SeedEventWithResource(seedContext);
            Assert.Equal(2, resource.MinimumStaff); // precondition

            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            var request = new MinimumStaffRequest { MinimumStaff = 5 };

            // Act
            var result = await service.UpdateMinimumStaff(resource.EventResourceId, request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(5, result.MinimumStaff);
            Assert.Equal(resource.EventResourceId, result.EventResourceId);

            // Verify in DB
            using var verifyContext = _fixture.CreateContext();
            var dbResource = await verifyContext.EventResource
                .AsNoTracking()
                .SingleAsync(r => r.EventResourceId == resource.EventResourceId);
            Assert.Equal(5, dbResource.MinimumStaff);
        }

        #endregion
    }
}
