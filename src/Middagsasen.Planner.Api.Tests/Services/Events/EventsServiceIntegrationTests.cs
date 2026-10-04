using Microsoft.EntityFrameworkCore;
using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services;
using Middagsasen.Planner.Api.Services.Events;
using Middagsasen.Planner.Api.Services.Resources;
using Middagsasen.Planner.Api.Tests.Infrastructure;
using NSubstitute;

namespace Middagsasen.Planner.Api.Tests.Services.Events
{
    [Collection("Database")]
    public class EventsServiceIntegrationTests
    {
        private readonly DatabaseFixture _fixture;

        // Fast «nå» før testdataene (januar 2026), så ressursene ikke er avsluttet.
        private static readonly TimeProvider Clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

        public EventsServiceIntegrationTests(DatabaseFixture fixture)
        {
            _fixture = fixture;
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
            return new EventsService(context, new ResourceReader(context, Clock), currentUser);
        }

        private static string UniqueName(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

        private async Task<User> SeedUser(PlannerDbContext context, string? firstName = null, bool isAdmin = false)
        {
            var user = new User
            {
                UserName = UniqueName("user"),
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
        public async Task CreateEvent_ResourceWithId_CreatesNewResource_AndLeavesExistingUntouched()
        {
            // Arrange: klienten sender med id-en til en eksisterende ressurs (f.eks. ved kopiering).
            using var seedContext = _fixture.CreateContext();
            var (existingEvent, existingResource) = await SeedEventWithResource(seedContext);

            using var context = _fixture.CreateContext();
            var request = new EventRequest
            {
                Name = UniqueName("CreateWithId"),
                StartTime = "2026-02-15T08:00:00",
                EndTime = "2026-02-15T16:00:00",
                Resources = new List<ResourceRequest>
                {
                    new ResourceRequest
                    {
                        Id = existingResource.EventResourceId,
                        ResourceTypeId = existingResource.ResourceTypeId,
                        StartTime = "2026-02-15T09:00:00",
                        EndTime = "2026-02-15T15:00:00",
                        MinimumStaff = 1,
                    }
                }
            };

            // Act
            var result = await CreateService(context).CreateEvent(request);

            // Assert
            var created = Assert.Single(result.Resources);
            Assert.NotEqual(existingResource.EventResourceId, created.Id);
            Assert.Equal(1, created.MinimumStaff);

            using var verifyContext = _fixture.CreateContext();
            var untouched = await verifyContext.EventResource.AsNoTracking()
                .SingleAsync(r => r.EventResourceId == existingResource.EventResourceId);
            Assert.Equal(existingEvent.EventId, untouched.EventId);
            Assert.Equal(2, untouched.MinimumStaff);
        }

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
            var (evt, resource) = await SeedEventWithResource(seedContext);

            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act
            var result = await service.DeleteEvent(evt.EventId);

            // Assert: svaret er det slettede arrangementet med ressursene.
            Assert.Equal(evt.EventId, result.Id);
            Assert.Equal(resource.EventResourceId, Assert.Single(result.Resources).Id);

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
        public async Task AddMessage_TrimsMessage()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var user = await SeedUser(seedContext);
            var (evt, resource) = await SeedEventWithResource(seedContext);

            using var context = _fixture.CreateContext();
            var service = CreateService(context, userId: user.UserId);

            var request = new MessageRequest
            {
                Message = "  Trimmet melding \n",
            };

            // Act
            var result = await service.AddMessage(resource.EventResourceId, user.UserId, request);

            // Assert
            Assert.Equal("Trimmet melding", result.Message);

            // Verify in DB
            using var verifyContext = _fixture.CreateContext();
            var dbMessage = await verifyContext.Messages
                .AsNoTracking()
                .SingleAsync(m => m.EventResourceMessageId == result.Id);
            Assert.Equal("Trimmet melding", dbMessage.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task AddMessage_ThrowsDomainValidation_WhenMessageIsBlank(string? text)
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var user = await SeedUser(seedContext);
            var (evt, resource) = await SeedEventWithResource(seedContext);

            using var context = _fixture.CreateContext();
            var service = CreateService(context, userId: user.UserId);

            // Act
            var ex = await Assert.ThrowsAsync<DomainValidationException>(
                () => service.AddMessage(resource.EventResourceId, user.UserId, new MessageRequest { Message = text! }));

            // Assert
            Assert.Equal(EventsService.MessageEmptyMessage, ex.Message);

            using var verifyContext = _fixture.CreateContext();
            Assert.False(await verifyContext.Messages.AnyAsync(m => m.EventResourceId == resource.EventResourceId));
        }

        [Fact]
        public async Task AddMessage_ThrowsDomainValidation_WhenMessageIsTooLong()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var user = await SeedUser(seedContext);
            var (evt, resource) = await SeedEventWithResource(seedContext);

            using var context = _fixture.CreateContext();
            var service = CreateService(context, userId: user.UserId);

            var request = new MessageRequest { Message = new string('a', MessageRequest.MaxLength + 1) };

            // Act
            var ex = await Assert.ThrowsAsync<DomainValidationException>(
                () => service.AddMessage(resource.EventResourceId, user.UserId, request));

            // Assert
            Assert.Equal(EventsService.MessageTooLongMessage, ex.Message);

            using var verifyContext = _fixture.CreateContext();
            Assert.False(await verifyContext.Messages.AnyAsync(m => m.EventResourceId == resource.EventResourceId));
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
        public async Task DeleteMessage_ThrowsEntityNotFound_WhenNotFound()
        {
            // Arrange
            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act & Assert
            await Assert.ThrowsAsync<EntityNotFoundException>(() => service.DeleteMessage(999999, 999999));
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

        #endregion
        #region Tilgang og flagg

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

        [Fact]
        public async Task GetEventById_ReturnsFlagsForOwner()
        {
            using var seedContext = _fixture.CreateContext();
            var owner = await SeedUser(seedContext, "Owner");
            var (evt, resource) = await SeedEventWithResource(seedContext);
            var shift = await SeedShift(seedContext, resource.EventResourceId, owner.UserId);

            using var context = _fixture.CreateContext();
            var service = CreateService(context, userId: owner.UserId);

            var result = await service.GetEventById(evt.EventId);

            var r = Assert.Single(result.Resources);
            Assert.True(r.IsMissingStaff);
            Assert.False(r.IsFull);
            Assert.False(r.IsPast);
            Assert.False(r.CanSignUp); // allerede påmeldt
            Assert.False(r.MustAnswerTraining); // ressurstypen har ikke opplæring
            Assert.Empty(r.CompetencyWarnings);
            var s = Assert.Single(r.Shifts);
            Assert.Equal(shift.EventResourceUserId, s.Id);
            Assert.True(s.IsMine);
            Assert.True(s.CanEdit);
            Assert.True(s.CanWithdraw);
            Assert.False(s.CanConfirmTraining);
            Assert.False(s.NeedsTraining);
        }

        [Fact]
        public async Task GetEvents_ReturnsFlagsForTrainer()
        {
            using var seedContext = _fixture.CreateContext();
            var owner = await SeedUser(seedContext, "Owner");
            var trainer = await SeedUser(seedContext, "Trainer");
            var (evt, resource) = await SeedEventWithResource(seedContext);
            await SeedTrainer(seedContext, resource.ResourceTypeId, trainer.UserId);
            seedContext.ResourceTypeTrainings.Add(new ResourceTypeTraining { UserId = owner.UserId, ResourceTypeId = resource.ResourceTypeId, TrainingComplete = false });
            await seedContext.SaveChangesAsync();
            await SeedShift(seedContext, resource.EventResourceId, owner.UserId);

            using var context = _fixture.CreateContext();
            var service = CreateService(context, userId: trainer.UserId);

            var result = await service.GetEvents(evt.StartTime.AddMinutes(-1), evt.StartTime.AddMinutes(1));

            var r = Assert.Single(Assert.Single(result, e => e.Id == evt.EventId).Resources);
            Assert.True(r.CanSignUp);
            Assert.True(r.MustAnswerTraining); // treneren har ikke svart på opplæring selv
            var s = Assert.Single(r.Shifts);
            Assert.False(s.IsMine);
            Assert.False(s.CanEdit);
            Assert.False(s.CanWithdraw);
            Assert.True(s.NeedsTraining);
            Assert.True(s.CanConfirmTraining);
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
