using Microsoft.EntityFrameworkCore;
using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services;
using Middagsasen.Planner.Api.Services.Events;
using Middagsasen.Planner.Api.Services.ResourceTypes;
using Middagsasen.Planner.Api.Services.SmsSender;
using Middagsasen.Planner.Api.Services.Storage;
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
        public async Task DeleteEvent_ThrowsEntityNotFound_WhenNotFound()
        {
            // Arrange
            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act & Assert
            await Assert.ThrowsAsync<EntityNotFoundException>(() => service.DeleteEvent(999999));
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
                Message = "Test message",
            };

            // Act
            var result = await service.AddMessage(resource.EventResourceId, user.UserId, request);

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
            // Innlogget bruker i servicen er bevisst en annen enn avsenderen som sendes inn.
            var service = CreateService(context, userId: 0);

            var request = new MessageRequest
            {
                Message = "CreatedBy test",
            };

            // Act
            var result = await service.AddMessage(resource.EventResourceId, user.UserId, request);

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
            // Forfatteren sletter sin egen melding.
            var service = CreateService(context, userId: user.UserId);

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

        #region Ressurstider over midnatt

        [Fact]
        public async Task CreateEventFromTemplate_PlacesResourcesNearestEvent_OverMidnight()
        {
            // Arrange — nattmal 22:00–06:00 med vakter 21:30–06:30 og 01:00–03:00
            using var seedContext = _fixture.CreateContext();
            var rt = await SeedResourceType(seedContext);
            var template = new EventTemplate
            {
                Name = UniqueName("NightTemplate"),
                EventName = UniqueName("Night"),
                StartTime = new DateTime(2000, 1, 1, 22, 0, 0),
                EndTime = new DateTime(2000, 1, 1, 6, 0, 0),
                ResourceTemplates = new List<ResourceTemplate>
                {
                    new ResourceTemplate { ResourceTypeId = rt.ResourceTypeId, StartTime = new DateTime(2000, 1, 1, 21, 30, 0), EndTime = new DateTime(2000, 1, 1, 6, 30, 0), MinimumStaff = 1 },
                    new ResourceTemplate { ResourceTypeId = rt.ResourceTypeId, StartTime = new DateTime(2000, 1, 1, 1, 0, 0), EndTime = new DateTime(2000, 1, 1, 3, 0, 0), MinimumStaff = 2 },
                }
            };
            seedContext.EventTemplates.Add(template);
            await seedContext.SaveChangesAsync();

            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act — nyttårsaften
            var result = await service.CreateEventFromTemplate(template.EventTemplateId, new EventFromTemplateRequest { StartDate = "2026-12-31" });

            // Assert
            using var verifyContext = _fixture.CreateContext();
            var dbEvent = await verifyContext.Events
                .Include(e => e.Resources)
                .AsNoTracking()
                .SingleAsync(e => e.EventId == result.Id);
            Assert.Equal(new DateTime(2026, 12, 31, 22, 0, 0), dbEvent.StartTime);
            Assert.Equal(new DateTime(2027, 1, 1, 6, 0, 0), dbEvent.EndTime);

            var night = dbEvent.Resources.Single(r => r.MinimumStaff == 1);
            Assert.Equal(new DateTime(2026, 12, 31, 21, 30, 0), night.StartTime);
            Assert.Equal(new DateTime(2027, 1, 1, 6, 30, 0), night.EndTime);

            var afterMidnight = dbEvent.Resources.Single(r => r.MinimumStaff == 2);
            Assert.Equal(new DateTime(2027, 1, 1, 1, 0, 0), afterMidnight.StartTime);
            Assert.Equal(new DateTime(2027, 1, 1, 3, 0, 0), afterMidnight.EndTime);
        }

        [Fact]
        public async Task CreateEvent_PlacesResourcesNearestEvent_IgnoringSubmittedDate()
        {
            // Arrange — vaktliste 22:00–06:00 der alle tider sendes med vaktlistedatoen
            using var seedContext = _fixture.CreateContext();
            var rt = await SeedResourceType(seedContext);

            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            var request = new EventRequest
            {
                Name = UniqueName("Night"),
                StartTime = "2026-01-15T22:00:00",
                EndTime = "2026-01-15T06:00:00",
                Resources = new List<ResourceRequest>
                {
                    new ResourceRequest { ResourceTypeId = rt.ResourceTypeId, StartTime = "2026-01-15T01:00:00", EndTime = "2026-01-15T03:00:00", MinimumStaff = 1 },
                }
            };

            // Act
            var result = await service.CreateEvent(request);

            // Assert
            using var verifyContext = _fixture.CreateContext();
            var dbEvent = await verifyContext.Events
                .Include(e => e.Resources)
                .AsNoTracking()
                .SingleAsync(e => e.EventId == result.Id);
            Assert.Equal(new DateTime(2026, 1, 16, 6, 0, 0), dbEvent.EndTime);
            var resource = Assert.Single(dbEvent.Resources);
            Assert.Equal(new DateTime(2026, 1, 16, 1, 0, 0), resource.StartTime);
            Assert.Equal(new DateTime(2026, 1, 16, 3, 0, 0), resource.EndTime);
        }

        [Fact]
        public async Task UpdateEvent_PlacesExistingResourceNearestEvent_OverMidnight()
        {
            // Arrange — eksisterende vaktliste 08:00–16:00 flyttes til natt
            using var seedContext = _fixture.CreateContext();
            var (evt, resource) = await SeedEventWithResource(seedContext);

            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            var request = new EventRequest
            {
                Name = evt.Name,
                StartTime = "2026-01-15T22:00:00",
                EndTime = "2026-01-16T06:00:00",
                Resources = new List<ResourceRequest>
                {
                    new ResourceRequest { Id = resource.EventResourceId, ResourceTypeId = resource.ResourceTypeId, StartTime = "2026-01-15T23:45:00", EndTime = "2026-01-15T02:00:00", MinimumStaff = 2 },
                }
            };

            // Act
            await service.UpdateEvent(evt.EventId, request);

            // Assert
            using var verifyContext = _fixture.CreateContext();
            var dbResource = await verifyContext.EventResource
                .AsNoTracking()
                .SingleAsync(r => r.EventResourceId == resource.EventResourceId);
            Assert.Equal(new DateTime(2026, 1, 15, 23, 45, 0), dbResource.StartTime);
            Assert.Equal(new DateTime(2026, 1, 16, 2, 0, 0), dbResource.EndTime);
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

        #region NotFound

        [Fact]
        public async Task GetEventById_ThrowsEntityNotFound_WhenNotFound()
        {
            // Arrange
            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act & Assert
            await Assert.ThrowsAsync<EntityNotFoundException>(() => service.GetEventById(999999));
        }

        [Fact]
        public async Task UpdateEvent_ThrowsEntityNotFound_WhenNotFound()
        {
            // Arrange
            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act & Assert
            await Assert.ThrowsAsync<EntityNotFoundException>(() => service.UpdateEvent(999999, new EventRequest { Name = "X", StartTime = "2026-01-15T08:00:00", EndTime = "2026-01-15T16:00:00", Resources = new List<ResourceRequest>() }));
        }

        [Fact]
        public async Task CreateEventFromTemplate_ThrowsEntityNotFound_WhenNotFound()
        {
            // Arrange
            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act & Assert
            await Assert.ThrowsAsync<EntityNotFoundException>(() => service.CreateEventFromTemplate(999999, new EventFromTemplateRequest { StartDate = "2026-01-15" }));
        }

        [Fact]
        public async Task UpdateShift_ThrowsEntityNotFound_WhenNotFound()
        {
            // Arrange
            using var context = _fixture.CreateContext();
            var service = CreateService(context, userId: 1, isAdmin: true);

            // Act & Assert
            await Assert.ThrowsAsync<EntityNotFoundException>(() => service.UpdateShift(999999, new ShiftRequest { UserId = 1 }));
        }

        [Fact]
        public async Task DeleteShift_ThrowsEntityNotFound_WhenNotFound()
        {
            // Arrange
            using var context = _fixture.CreateContext();
            var service = CreateService(context, userId: 1, isAdmin: true);

            // Act & Assert
            await Assert.ThrowsAsync<EntityNotFoundException>(() => service.DeleteShift(999999));
        }

        [Fact]
        public async Task DeleteMessage_ThrowsEntityNotFound_WhenNotFound()
        {
            // Arrange
            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act & Assert
            await Assert.ThrowsAsync<EntityNotFoundException>(() => service.DeleteMessage(999999, 999999));
        }

        [Fact]
        public async Task UpdateMinimumStaff_ThrowsEntityNotFound_WhenNotFound()
        {
            // Arrange
            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act & Assert
            await Assert.ThrowsAsync<EntityNotFoundException>(() => service.UpdateMinimumStaff(999999, new MinimumStaffRequest { MinimumStaff = 1 }));
        }

        [Fact]
        public async Task AddShift_ThrowsEntityNotFound_WhenEventResourceDoesNotExist()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var user = await SeedUser(seedContext);

            using var context = _fixture.CreateContext();
            var service = CreateService(context, userId: user.UserId);

            // Act & Assert
            await Assert.ThrowsAsync<EntityNotFoundException>(
                () => service.AddShift(999999, new ShiftRequest { UserId = user.UserId }));
        }

        [Fact]
        public async Task AddMessage_ThrowsEntityNotFound_WhenEventResourceDoesNotExist()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var user = await SeedUser(seedContext);

            using var context = _fixture.CreateContext();
            var service = CreateService(context, userId: user.UserId);

            // Act & Assert
            await Assert.ThrowsAsync<EntityNotFoundException>(
                () => service.AddMessage(999999, user.UserId, new MessageRequest { Message = "Hei" }));
        }

        [Fact]
        public async Task AddShift_DoesNotPersistShift_WhenTrainingFails()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var user = await SeedUser(seedContext);
            var (_, resource) = await SeedEventWithResource(seedContext);

            _resourceTypesService
                .UpdateTraining(Arg.Any<int>(), Arg.Any<TrainingRequest>())
                .Returns<TrainingResponse?>(_ => throw new EntityNotFoundException("Fant ikke opplæringen."));

            using var context = _fixture.CreateContext();
            var service = CreateService(context, userId: user.UserId);

            var request = new ShiftRequest
            {
                UserId = user.UserId,
                Comment = UniqueName("Rollback"),
                Training = new TrainingRequest { Id = 999999, ResourceTypeId = resource.ResourceTypeId, UserId = user.UserId, TrainingCompleted = true },
            };

            // Act
            await Assert.ThrowsAsync<EntityNotFoundException>(() => service.AddShift(resource.EventResourceId, request));

            // Assert
            using var verifyContext = _fixture.CreateContext();
            Assert.False(await verifyContext.Shifts.AnyAsync(s => s.Comment == request.Comment));
        }

        [Fact]
        public async Task UpdateShift_DoesNotPersistShiftChanges_WhenTrainingFails()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var user = await SeedUser(seedContext);
            var (_, resource) = await SeedEventWithResource(seedContext);

            var shift = new EventResourceUser
            {
                EventResourceId = resource.EventResourceId,
                UserId = user.UserId,
                Comment = "Original",
            };
            seedContext.Shifts.Add(shift);
            await seedContext.SaveChangesAsync();

            _resourceTypesService
                .UpdateTraining(Arg.Any<int>(), Arg.Any<TrainingRequest>())
                .Returns<TrainingResponse?>(_ => throw new EntityNotFoundException("Fant ikke opplæringen."));

            using var context = _fixture.CreateContext();
            var service = CreateService(context, userId: user.UserId);

            var request = new ShiftRequest
            {
                UserId = user.UserId,
                Comment = "Endret",
                Training = new TrainingRequest { Id = 999999, ResourceTypeId = resource.ResourceTypeId, UserId = user.UserId, TrainingCompleted = true },
            };

            // Act
            await Assert.ThrowsAsync<EntityNotFoundException>(() => service.UpdateShift(shift.EventResourceUserId, request));

            // Assert
            using var verifyContext = _fixture.CreateContext();
            var dbShift = await verifyContext.Shifts.AsNoTracking().SingleAsync(s => s.EventResourceUserId == shift.EventResourceUserId);
            Assert.Equal("Original", dbShift.Comment);
        }

        #endregion
        #region Tilgang (#94)

        private async Task<EventResourceUser> SeedShift(PlannerDbContext context, int eventResourceId, int userId, string comment = "Original")
        {
            var shift = new EventResourceUser
            {
                EventResourceId = eventResourceId,
                UserId = userId,
                StartTime = new DateTime(2026, 1, 15, 9, 0, 0),
                EndTime = new DateTime(2026, 1, 15, 15, 0, 0),
                Comment = comment,
            };
            context.Shifts.Add(shift);
            await context.SaveChangesAsync();
            return shift;
        }

        private static async Task SeedTrainer(PlannerDbContext context, int resourceTypeId, int userId)
        {
            context.ResourceTypeTrainers.Add(new ResourceTypeTrainer { ResourceTypeId = resourceTypeId, UserId = userId });
            await context.SaveChangesAsync();
        }

        private async Task<EventResourceUser> GetShiftFromDb(int shiftId)
        {
            using var verifyContext = _fixture.CreateContext();
            return await verifyContext.Shifts.AsNoTracking().SingleAsync(s => s.EventResourceUserId == shiftId);
        }

        [Fact]
        public async Task UpdateShift_Owner_CanUpdateTimesCommentAndOwnTraining()
        {
            using var seedContext = _fixture.CreateContext();
            var owner = await SeedUser(seedContext, "Owner");
            var (_, resource) = await SeedEventWithResource(seedContext);
            var shift = await SeedShift(seedContext, resource.EventResourceId, owner.UserId);

            using var context = _fixture.CreateContext();
            var service = CreateService(context, userId: owner.UserId);

            var training = new TrainingRequest { ResourceTypeId = resource.ResourceTypeId, UserId = owner.UserId, TrainingCompleted = false };
            var request = new ShiftRequest
            {
                UserId = owner.UserId,
                StartTime = new DateTime(2026, 1, 15, 10, 0, 0),
                EndTime = new DateTime(2026, 1, 15, 14, 0, 0),
                Comment = "Eier endret",
                Training = training,
            };

            var result = await service.UpdateShift(shift.EventResourceUserId, request);

            Assert.Equal("Eier endret", result.Comment);
            var dbShift = await GetShiftFromDb(shift.EventResourceUserId);
            Assert.Equal("Eier endret", dbShift.Comment);
            Assert.Equal(new DateTime(2026, 1, 15, 10, 0, 0), dbShift.StartTime);
            Assert.Equal(new DateTime(2026, 1, 15, 14, 0, 0), dbShift.EndTime);
            await _resourceTypesService.Received(1).CreateTraining(resource.ResourceTypeId, training);
        }

        [Fact]
        public async Task UpdateShift_Owner_ThrowsForbidden_WhenMovingShiftToOtherUser()
        {
            using var seedContext = _fixture.CreateContext();
            var owner = await SeedUser(seedContext, "Owner");
            var otherUser = await SeedUser(seedContext, "Other");
            var (_, resource) = await SeedEventWithResource(seedContext);
            var shift = await SeedShift(seedContext, resource.EventResourceId, owner.UserId);

            using var context = _fixture.CreateContext();
            var service = CreateService(context, userId: owner.UserId);

            var request = new ShiftRequest { UserId = otherUser.UserId, Comment = "Flyttet" };

            await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.UpdateShift(shift.EventResourceUserId, request));

            var dbShift = await GetShiftFromDb(shift.EventResourceUserId);
            Assert.Equal(owner.UserId, dbShift.UserId);
            Assert.Equal("Original", dbShift.Comment);
        }

        [Fact]
        public async Task UpdateShift_Owner_ThrowsForbidden_WhenTrainingIsForOtherUser()
        {
            using var seedContext = _fixture.CreateContext();
            var owner = await SeedUser(seedContext, "Owner");
            var otherUser = await SeedUser(seedContext, "Other");
            var (_, resource) = await SeedEventWithResource(seedContext);
            var shift = await SeedShift(seedContext, resource.EventResourceId, owner.UserId);

            using var context = _fixture.CreateContext();
            var service = CreateService(context, userId: owner.UserId);

            var request = new ShiftRequest
            {
                UserId = owner.UserId,
                Comment = "Endret",
                Training = new TrainingRequest { ResourceTypeId = resource.ResourceTypeId, UserId = otherUser.UserId, TrainingCompleted = true },
            };

            await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.UpdateShift(shift.EventResourceUserId, request));

            Assert.Equal("Original", (await GetShiftFromDb(shift.EventResourceUserId)).Comment);
            await _resourceTypesService.DidNotReceiveWithAnyArgs().CreateTraining(default, default!);
        }

        [Fact]
        public async Task UpdateShift_ThrowsForbidden_WhenOtherUserUpdatesShift()
        {
            using var seedContext = _fixture.CreateContext();
            var owner = await SeedUser(seedContext, "Owner");
            var otherUser = await SeedUser(seedContext, "Other");
            var (_, resource) = await SeedEventWithResource(seedContext);
            var shift = await SeedShift(seedContext, resource.EventResourceId, owner.UserId);

            using var context = _fixture.CreateContext();
            var service = CreateService(context, userId: otherUser.UserId);

            // Både med request.UserId = innlogget bruker (forsøk på å ta over vakta) og = eieren.
            await Assert.ThrowsAsync<ForbiddenAccessException>(
                () => service.UpdateShift(shift.EventResourceUserId, new ShiftRequest { UserId = otherUser.UserId, Comment = "Tatt over" }));
            await Assert.ThrowsAsync<ForbiddenAccessException>(
                () => service.UpdateShift(shift.EventResourceUserId, new ShiftRequest { UserId = owner.UserId, Comment = "Endret" }));

            var dbShift = await GetShiftFromDb(shift.EventResourceUserId);
            Assert.Equal(owner.UserId, dbShift.UserId);
            Assert.Equal("Original", dbShift.Comment);
        }

        [Fact]
        public async Task UpdateShift_Trainer_UpdatesTrainingForOwner_WithoutChangingShift()
        {
            using var seedContext = _fixture.CreateContext();
            var owner = await SeedUser(seedContext, "Owner");
            var trainer = await SeedUser(seedContext, "Trainer");
            var (_, resource) = await SeedEventWithResource(seedContext);
            await SeedTrainer(seedContext, resource.ResourceTypeId, trainer.UserId);
            var shift = await SeedShift(seedContext, resource.EventResourceId, owner.UserId);

            using var context = _fixture.CreateContext();
            var service = CreateService(context, userId: trainer.UserId);

            var training = new TrainingRequest { Id = 4242, ResourceTypeId = resource.ResourceTypeId, UserId = owner.UserId, TrainingCompleted = true };
            // Frontend sender hele vakt-objektet; vaktfeltene skal ignoreres for treneren.
            var request = new ShiftRequest
            {
                UserId = trainer.UserId,
                StartTime = new DateTime(2026, 1, 15, 11, 0, 0),
                EndTime = new DateTime(2026, 1, 15, 12, 0, 0),
                Comment = "Trener endret",
                Training = training,
            };

            var result = await service.UpdateShift(shift.EventResourceUserId, request);

            Assert.Equal(owner.UserId, result.User.Id);
            await _resourceTypesService.Received(1).UpdateTraining(resource.ResourceTypeId, training);

            var dbShift = await GetShiftFromDb(shift.EventResourceUserId);
            Assert.Equal(owner.UserId, dbShift.UserId);
            Assert.Equal("Original", dbShift.Comment);
            Assert.Equal(new DateTime(2026, 1, 15, 9, 0, 0), dbShift.StartTime);
            Assert.Equal(new DateTime(2026, 1, 15, 15, 0, 0), dbShift.EndTime);
        }

        [Fact]
        public async Task UpdateShift_Trainer_WithoutTraining_ReturnsShiftUnchanged()
        {
            using var seedContext = _fixture.CreateContext();
            var owner = await SeedUser(seedContext, "Owner");
            var trainer = await SeedUser(seedContext, "Trainer");
            var (_, resource) = await SeedEventWithResource(seedContext);
            await SeedTrainer(seedContext, resource.ResourceTypeId, trainer.UserId);
            var shift = await SeedShift(seedContext, resource.EventResourceId, owner.UserId);

            using var context = _fixture.CreateContext();
            var service = CreateService(context, userId: trainer.UserId);

            var result = await service.UpdateShift(shift.EventResourceUserId, new ShiftRequest { UserId = trainer.UserId, Comment = "Trener endret" });

            Assert.Equal(owner.UserId, result.User.Id);
            Assert.Equal("Original", result.Comment);
            Assert.Equal("Original", (await GetShiftFromDb(shift.EventResourceUserId)).Comment);
        }

        [Fact]
        public async Task UpdateShift_Trainer_ThrowsForbidden_WhenTrainingIsForOtherUserThanOwner()
        {
            using var seedContext = _fixture.CreateContext();
            var owner = await SeedUser(seedContext, "Owner");
            var trainer = await SeedUser(seedContext, "Trainer");
            var otherUser = await SeedUser(seedContext, "Other");
            var (_, resource) = await SeedEventWithResource(seedContext);
            await SeedTrainer(seedContext, resource.ResourceTypeId, trainer.UserId);
            var shift = await SeedShift(seedContext, resource.EventResourceId, owner.UserId);

            using var context = _fixture.CreateContext();
            var service = CreateService(context, userId: trainer.UserId);

            var request = new ShiftRequest
            {
                UserId = owner.UserId,
                Training = new TrainingRequest { ResourceTypeId = resource.ResourceTypeId, UserId = otherUser.UserId, TrainingCompleted = true },
            };

            await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.UpdateShift(shift.EventResourceUserId, request));
            await _resourceTypesService.DidNotReceiveWithAnyArgs().CreateTraining(default, default!);
        }

        [Fact]
        public async Task UpdateShift_Trainer_ThrowsForbidden_WhenTrainingIsForOtherResourceType()
        {
            using var seedContext = _fixture.CreateContext();
            var owner = await SeedUser(seedContext, "Owner");
            var trainer = await SeedUser(seedContext, "Trainer");
            var (_, resource) = await SeedEventWithResource(seedContext);
            var otherRt = await SeedResourceType(seedContext);
            await SeedTrainer(seedContext, resource.ResourceTypeId, trainer.UserId);
            await SeedTrainer(seedContext, otherRt.ResourceTypeId, trainer.UserId);
            var shift = await SeedShift(seedContext, resource.EventResourceId, owner.UserId);

            using var context = _fixture.CreateContext();
            var service = CreateService(context, userId: trainer.UserId);

            var request = new ShiftRequest
            {
                UserId = owner.UserId,
                Training = new TrainingRequest { ResourceTypeId = otherRt.ResourceTypeId, UserId = owner.UserId, TrainingCompleted = true },
            };

            await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.UpdateShift(shift.EventResourceUserId, request));
            await _resourceTypesService.DidNotReceiveWithAnyArgs().CreateTraining(default, default!);
        }

        [Fact]
        public async Task UpdateShift_ThrowsForbidden_WhenTrainerForOtherResourceType()
        {
            using var seedContext = _fixture.CreateContext();
            var owner = await SeedUser(seedContext, "Owner");
            var trainer = await SeedUser(seedContext, "Trainer");
            var (_, resource) = await SeedEventWithResource(seedContext);
            var otherRt = await SeedResourceType(seedContext);
            await SeedTrainer(seedContext, otherRt.ResourceTypeId, trainer.UserId);
            var shift = await SeedShift(seedContext, resource.EventResourceId, owner.UserId);

            using var context = _fixture.CreateContext();
            var service = CreateService(context, userId: trainer.UserId);

            var request = new ShiftRequest
            {
                UserId = owner.UserId,
                Training = new TrainingRequest { ResourceTypeId = resource.ResourceTypeId, UserId = owner.UserId, TrainingCompleted = true },
            };

            await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.UpdateShift(shift.EventResourceUserId, request));
            await _resourceTypesService.DidNotReceiveWithAnyArgs().CreateTraining(default, default!);
        }

        [Fact]
        public async Task UpdateShift_Admin_CanMoveShiftAndUpdateAllFields()
        {
            using var seedContext = _fixture.CreateContext();
            var admin = await SeedUser(seedContext, "Admin", isAdmin: true);
            var owner = await SeedUser(seedContext, "Owner");
            var newOwner = await SeedUser(seedContext, "NewOwner");
            var (_, resource) = await SeedEventWithResource(seedContext);
            var shift = await SeedShift(seedContext, resource.EventResourceId, owner.UserId);

            using var context = _fixture.CreateContext();
            var service = CreateService(context, userId: admin.UserId, isAdmin: true);

            var training = new TrainingRequest { ResourceTypeId = resource.ResourceTypeId, UserId = newOwner.UserId, TrainingCompleted = true };
            var request = new ShiftRequest
            {
                UserId = newOwner.UserId,
                StartTime = new DateTime(2026, 1, 15, 8, 0, 0),
                EndTime = new DateTime(2026, 1, 15, 16, 0, 0),
                Comment = "Admin endret",
                Training = training,
            };

            var result = await service.UpdateShift(shift.EventResourceUserId, request);

            Assert.Equal(newOwner.UserId, result.User.Id);
            await _resourceTypesService.Received(1).CreateTraining(resource.ResourceTypeId, training);
            var dbShift = await GetShiftFromDb(shift.EventResourceUserId);
            Assert.Equal(newOwner.UserId, dbShift.UserId);
            Assert.Equal("Admin endret", dbShift.Comment);
            Assert.Equal(new DateTime(2026, 1, 15, 8, 0, 0), dbShift.StartTime);
            Assert.Equal(new DateTime(2026, 1, 15, 16, 0, 0), dbShift.EndTime);
        }

        [Fact]
        public async Task UpdateShift_DoesNotPersistShiftChanges_WhenTrainingIsForbidden()
        {
            using var seedContext = _fixture.CreateContext();
            var owner = await SeedUser(seedContext, "Owner");
            var (_, resource) = await SeedEventWithResource(seedContext);
            var shift = await SeedShift(seedContext, resource.EventResourceId, owner.UserId);

            _resourceTypesService
                .CreateTraining(Arg.Any<int>(), Arg.Any<TrainingRequest>())
                .Returns<TrainingResponse?>(_ => throw new ForbiddenAccessException());

            using var context = _fixture.CreateContext();
            var service = CreateService(context, userId: owner.UserId);

            var request = new ShiftRequest
            {
                UserId = owner.UserId,
                Comment = "Endret",
                Training = new TrainingRequest { ResourceTypeId = resource.ResourceTypeId, UserId = owner.UserId, TrainingCompleted = true },
            };

            await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.UpdateShift(shift.EventResourceUserId, request));

            Assert.Equal("Original", (await GetShiftFromDb(shift.EventResourceUserId)).Comment);
        }

        [Fact]
        public async Task AddShift_DoesNotPersistShift_WhenTrainingIsForbidden()
        {
            using var seedContext = _fixture.CreateContext();
            var user = await SeedUser(seedContext);
            var (_, resource) = await SeedEventWithResource(seedContext);

            _resourceTypesService
                .CreateTraining(Arg.Any<int>(), Arg.Any<TrainingRequest>())
                .Returns<TrainingResponse?>(_ => throw new ForbiddenAccessException());

            using var context = _fixture.CreateContext();
            var service = CreateService(context, userId: user.UserId);

            var request = new ShiftRequest
            {
                UserId = user.UserId,
                Comment = UniqueName("Rollback403"),
                Training = new TrainingRequest { ResourceTypeId = resource.ResourceTypeId, UserId = user.UserId, TrainingCompleted = true },
            };

            await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.AddShift(resource.EventResourceId, request));

            using var verifyContext = _fixture.CreateContext();
            Assert.False(await verifyContext.Shifts.AnyAsync(s => s.Comment == request.Comment));
        }

        [Fact]
        public async Task AddShift_ThrowsForbidden_WhenNonAdminSendsTrainingForOtherUser()
        {
            using var seedContext = _fixture.CreateContext();
            var user = await SeedUser(seedContext, "Current");
            var otherUser = await SeedUser(seedContext, "Other");
            var (_, resource) = await SeedEventWithResource(seedContext);

            using var context = _fixture.CreateContext();
            var service = CreateService(context, userId: user.UserId);

            var request = new ShiftRequest
            {
                UserId = user.UserId,
                Comment = UniqueName("AddTrainingOther"),
                Training = new TrainingRequest { ResourceTypeId = resource.ResourceTypeId, UserId = otherUser.UserId, TrainingCompleted = true },
            };

            await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.AddShift(resource.EventResourceId, request));

            using var verifyContext = _fixture.CreateContext();
            Assert.False(await verifyContext.Shifts.AnyAsync(s => s.Comment == request.Comment));
            await _resourceTypesService.DidNotReceiveWithAnyArgs().CreateTraining(default, default!);
        }

        [Fact]
        public async Task UpdateShift_Trainer_ConfirmsOwnersTraining_WithRealResourceTypesService()
        {
            using var seedContext = _fixture.CreateContext();
            var owner = await SeedUser(seedContext, "Owner");
            var trainer = await SeedUser(seedContext, "Trainer");
            var (_, resource) = await SeedEventWithResource(seedContext);
            await SeedTrainer(seedContext, resource.ResourceTypeId, trainer.UserId);
            var shift = await SeedShift(seedContext, resource.EventResourceId, owner.UserId);

            using var context = _fixture.CreateContext();
            var currentUser = MockCurrentUser(trainer.UserId);
            var resourceTypesService = new ResourceTypesService(
                context,
                Substitute.For<ISmsSender>(),
                Substitute.For<IStorageService>(),
                currentUser);
            var service = new EventsService(context, resourceTypesService, currentUser);

            var request = new ShiftRequest
            {
                UserId = owner.UserId,
                Comment = "Ignoreres",
                Training = new TrainingRequest { ResourceTypeId = resource.ResourceTypeId, UserId = owner.UserId, TrainingCompleted = true },
            };

            await service.UpdateShift(shift.EventResourceUserId, request);

            using var verifyContext = _fixture.CreateContext();
            var dbTraining = await verifyContext.ResourceTypeTrainings.AsNoTracking()
                .SingleAsync(t => t.UserId == owner.UserId && t.ResourceTypeId == resource.ResourceTypeId);
            Assert.True(dbTraining.TrainingComplete);
            Assert.Equal(trainer.UserId, dbTraining.ConfirmedBy);
            Assert.Equal("Original", (await GetShiftFromDb(shift.EventResourceUserId)).Comment);
        }

        [Fact]
        public async Task DeleteMessage_AllowsAdmin_ToDeleteOthersMessage()
        {
            using var seedContext = _fixture.CreateContext();
            var author = await SeedUser(seedContext, "Author");
            var admin = await SeedUser(seedContext, "Admin", isAdmin: true);
            var (_, resource) = await SeedEventWithResource(seedContext);
            var message = new EventResourceMessage { EventResourceId = resource.EventResourceId, Message = "Admin sletter", Created = DateTime.UtcNow, CreatedBy = author.UserId };
            seedContext.Messages.Add(message);
            await seedContext.SaveChangesAsync();

            using var context = _fixture.CreateContext();
            var service = CreateService(context, userId: admin.UserId, isAdmin: true);

            await service.DeleteMessage(message.EventResourceMessageId, resource.EventResourceId);

            using var verifyContext = _fixture.CreateContext();
            Assert.False(await verifyContext.Messages.AnyAsync(m => m.EventResourceMessageId == message.EventResourceMessageId));
        }

        [Fact]
        public async Task DeleteMessage_ThrowsForbidden_WhenOtherUserDeletesMessage()
        {
            using var seedContext = _fixture.CreateContext();
            var author = await SeedUser(seedContext, "Author");
            var otherUser = await SeedUser(seedContext, "Other");
            var (_, resource) = await SeedEventWithResource(seedContext);
            var message = new EventResourceMessage { EventResourceId = resource.EventResourceId, Message = "Skal ikke slettes", Created = DateTime.UtcNow, CreatedBy = author.UserId };
            seedContext.Messages.Add(message);
            await seedContext.SaveChangesAsync();

            using var context = _fixture.CreateContext();
            var service = CreateService(context, userId: otherUser.UserId);

            await Assert.ThrowsAsync<ForbiddenAccessException>(
                () => service.DeleteMessage(message.EventResourceMessageId, resource.EventResourceId));

            using var verifyContext = _fixture.CreateContext();
            Assert.True(await verifyContext.Messages.AnyAsync(m => m.EventResourceMessageId == message.EventResourceMessageId));
        }

        #endregion

    }
}
