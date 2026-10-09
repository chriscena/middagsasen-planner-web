using Microsoft.EntityFrameworkCore;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services;
using Middagsasen.Planner.Api.Services.Events;
using Middagsasen.Planner.Api.Services.Resources;
using Middagsasen.Planner.Api.Tests.Infrastructure;

namespace Middagsasen.Planner.Api.Tests.Services.Events
{
    [Collection("Database")]
    public class EventTemplatesServiceIntegrationTests
    {
        private readonly DatabaseFixture _fixture;

        public EventTemplatesServiceIntegrationTests(DatabaseFixture fixture)
        {
            _fixture = fixture;
        }

        private EventTemplatesService CreateService(PlannerDbContext context)
        {
            return new EventTemplatesService(context, new ResourceReader(context, TimeProvider.System));
        }

        private static string UniqueName(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

        private async Task<ResourceType> SeedResourceType(PlannerDbContext context, string? name = null)
        {
            var rt = new ResourceType
            {
                Name = name ?? UniqueName("RT"),
                DefaultShiftCount = 2,
            };
            context.ResourceTypes.Add(rt);
            await context.SaveChangesAsync();
            return rt;
        }

        private async Task<(Event evt, EventResource resource)> SeedEventWithResource(PlannerDbContext context, int resourceTypeId)
        {
            var evt = new Event
            {
                Name = UniqueName("Event"),
                StartTime = new DateTime(2026, 1, 15, 8, 0, 0),
                EndTime = new DateTime(2026, 1, 15, 16, 0, 0),
                Resources = new List<EventResource>
                {
                    new EventResource
                    {
                        ResourceTypeId = resourceTypeId,
                        StartTime = new DateTime(2026, 1, 15, 9, 0, 0),
                        EndTime = new DateTime(2026, 1, 15, 15, 0, 0),
                        ShiftCount = 3,
                    }
                }
            };
            context.Events.Add(evt);
            await context.SaveChangesAsync();
            return (evt, evt.Resources.First());
        }

        #region Template CRUD

        [Fact]
        public async Task CreateEventTemplate_PersistsToDatabase()
        {
            // Arrange
            var templateName = UniqueName("Template");
            var eventName = UniqueName("EventName");
            using var seedContext = _fixture.CreateContext();
            var rt = await SeedResourceType(seedContext);

            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            var request = new EventTemplateRequest
            {
                Name = templateName,
                EventName = eventName,
                StartTime = new TimeOnly(8, 0),
                EndTime = new TimeOnly(16, 0),
                ResourceTemplates = new List<ResourceTemplateRequest>
                {
                    new ResourceTemplateRequest
                    {
                        ResourceTypeId = rt.ResourceTypeId,
                        StartTime = new TimeOnly(9, 0),
                        EndTime = new TimeOnly(15, 0),
                        ShiftCount = 3,
                    }
                }
            };

            // Act
            var result = await service.CreateEventTemplate(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(templateName, result.Name);
            Assert.Equal(eventName, result.EventName);

            // Verify in DB with fresh context
            using var verifyContext = _fixture.CreateContext();
            var dbTemplate = await verifyContext.EventTemplates
                .Include(t => t.ResourceTemplates)
                .AsNoTracking()
                .SingleOrDefaultAsync(t => t.EventTemplateId == result.Id);
            Assert.NotNull(dbTemplate);
            Assert.Equal(templateName, dbTemplate.Name);
            Assert.Equal(eventName, dbTemplate.EventName);
            Assert.Single(dbTemplate.ResourceTemplates);
            Assert.Equal(3, dbTemplate.ResourceTemplates.First().ShiftCount);
        }

        [Fact]
        public async Task GetEventTemplates_ReturnsAllTemplates()
        {
            // Arrange
            var name1 = UniqueName("List1");
            var name2 = UniqueName("List2");
            using var seedContext = _fixture.CreateContext();
            var rt = await SeedResourceType(seedContext);

            seedContext.EventTemplates.Add(new EventTemplate
            {
                Name = name1,
                EventName = UniqueName("EN1"),
                StartTime = new DateTime(2026, 1, 15, 8, 0, 0),
                EndTime = new DateTime(2026, 1, 15, 16, 0, 0),
                ResourceTemplates = new List<ResourceTemplate>
                {
                    new ResourceTemplate { ResourceTypeId = rt.ResourceTypeId, StartTime = new DateTime(2026, 1, 15, 8, 0, 0), EndTime = new DateTime(2026, 1, 15, 16, 0, 0), ShiftCount = 1 }
                }
            });
            seedContext.EventTemplates.Add(new EventTemplate
            {
                Name = name2,
                EventName = UniqueName("EN2"),
                StartTime = new DateTime(2026, 2, 15, 8, 0, 0),
                EndTime = new DateTime(2026, 2, 15, 16, 0, 0),
                ResourceTemplates = new List<ResourceTemplate>
                {
                    new ResourceTemplate { ResourceTypeId = rt.ResourceTypeId, StartTime = new DateTime(2026, 2, 15, 8, 0, 0), EndTime = new DateTime(2026, 2, 15, 16, 0, 0), ShiftCount = 1 }
                }
            });
            await seedContext.SaveChangesAsync();

            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act
            var results = (await service.GetEventTemplates()).ToList();

            // Assert
            Assert.Contains(results, r => r.Name == name1);
            Assert.Contains(results, r => r.Name == name2);
        }

        [Fact]
        public async Task GetEventTemplateById_ReturnsCorrectTemplate()
        {
            // Arrange
            var templateName = UniqueName("GetById");
            var eventName = UniqueName("EventGetById");
            using var seedContext = _fixture.CreateContext();
            var rt = await SeedResourceType(seedContext);

            var template = new EventTemplate
            {
                Name = templateName,
                EventName = eventName,
                StartTime = new DateTime(2026, 3, 10, 10, 0, 0),
                EndTime = new DateTime(2026, 3, 10, 18, 0, 0),
                ResourceTemplates = new List<ResourceTemplate>
                {
                    new ResourceTemplate
                    {
                        ResourceTypeId = rt.ResourceTypeId,
                        StartTime = new DateTime(2026, 3, 10, 10, 0, 0),
                        EndTime = new DateTime(2026, 3, 10, 18, 0, 0),
                        ShiftCount = 4,
                    }
                }
            };
            seedContext.EventTemplates.Add(template);
            await seedContext.SaveChangesAsync();

            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act
            var result = await service.GetEventTemplateById(template.EventTemplateId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(templateName, result.Name);
            Assert.Equal(eventName, result.EventName);
            Assert.Equal("2026-03-10T10:00", result.StartTime);
            Assert.Equal("2026-03-10T18:00", result.EndTime);
            Assert.NotNull(result.ResourceTemplates);
            Assert.Single(result.ResourceTemplates);
            var resourceTemplate = result.ResourceTemplates.First();
            Assert.Equal(rt.ResourceTypeId, resourceTemplate.ResourceType.Id);
            Assert.Equal(4, resourceTemplate.ShiftCount);
        }

        [Fact]
        public async Task GetEventTemplateById_ThrowsEntityNotFound_WhenNotFound()
        {
            // Arrange
            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act & Assert
            await Assert.ThrowsAsync<EntityNotFoundException>(() => service.GetEventTemplateById(999999));
        }

        [Fact]
        public async Task UpdateEventTemplate_UpdatesProperties()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var rt = await SeedResourceType(seedContext);

            var template = new EventTemplate
            {
                Name = UniqueName("Original"),
                EventName = UniqueName("OriginalEvent"),
                StartTime = new DateTime(2026, 1, 15, 8, 0, 0),
                EndTime = new DateTime(2026, 1, 15, 16, 0, 0),
                ResourceTemplates = new List<ResourceTemplate>
                {
                    new ResourceTemplate
                    {
                        ResourceTypeId = rt.ResourceTypeId,
                        StartTime = new DateTime(2026, 1, 15, 8, 0, 0),
                        EndTime = new DateTime(2026, 1, 15, 16, 0, 0),
                        ShiftCount = 2,
                    }
                }
            };
            seedContext.EventTemplates.Add(template);
            await seedContext.SaveChangesAsync();
            var resourceTemplateId = template.ResourceTemplates.First().ResourceTemplateId;

            var updatedName = UniqueName("Updated");
            var updatedEventName = UniqueName("UpdatedEvent");

            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            var request = new EventTemplateRequest
            {
                Name = updatedName,
                EventName = updatedEventName,
                StartTime = new TimeOnly(10, 0),
                EndTime = new TimeOnly(18, 0),
                ResourceTemplates = new List<ResourceTemplateRequest>
                {
                    new ResourceTemplateRequest
                    {
                        Id = resourceTemplateId,
                        ResourceTypeId = rt.ResourceTypeId,
                        StartTime = new TimeOnly(10, 0),
                        EndTime = new TimeOnly(18, 0),
                        ShiftCount = 2,
                    }
                }
            };

            // Act
            var result = await service.UpdateEventTemplate(template.EventTemplateId, request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(updatedName, result.Name);
            Assert.Equal(updatedEventName, result.EventName);
            // Bare klokkeslettet sendes; det lagres på malens faste referansedato.
            Assert.Equal("2000-01-01T10:00", result.StartTime);
            Assert.Equal("2000-01-01T18:00", result.EndTime);

            // Verify in DB
            using var verifyContext = _fixture.CreateContext();
            var dbTemplate = await verifyContext.EventTemplates
                .AsNoTracking()
                .SingleAsync(t => t.EventTemplateId == template.EventTemplateId);
            Assert.Equal(updatedName, dbTemplate.Name);
            Assert.Equal(updatedEventName, dbTemplate.EventName);
        }

        [Fact]
        public async Task UpdateEventTemplate_CanAddResourceTemplate()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var rt1 = await SeedResourceType(seedContext, UniqueName("RT1"));
            var rt2 = await SeedResourceType(seedContext, UniqueName("RT2"));

            var template = new EventTemplate
            {
                Name = UniqueName("AddRes"),
                EventName = UniqueName("AddResEvent"),
                StartTime = new DateTime(2026, 1, 15, 8, 0, 0),
                EndTime = new DateTime(2026, 1, 15, 16, 0, 0),
                ResourceTemplates = new List<ResourceTemplate>
                {
                    new ResourceTemplate
                    {
                        ResourceTypeId = rt1.ResourceTypeId,
                        StartTime = new DateTime(2026, 1, 15, 8, 0, 0),
                        EndTime = new DateTime(2026, 1, 15, 16, 0, 0),
                        ShiftCount = 2,
                    }
                }
            };
            seedContext.EventTemplates.Add(template);
            await seedContext.SaveChangesAsync();
            var existingResourceTemplateId = template.ResourceTemplates.First().ResourceTemplateId;

            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            var request = new EventTemplateRequest
            {
                Name = template.Name,
                EventName = template.EventName,
                StartTime = new TimeOnly(8, 0),
                EndTime = new TimeOnly(16, 0),
                ResourceTemplates = new List<ResourceTemplateRequest>
                {
                    // Keep existing
                    new ResourceTemplateRequest
                    {
                        Id = existingResourceTemplateId,
                        ResourceTypeId = rt1.ResourceTypeId,
                        StartTime = new TimeOnly(8, 0),
                        EndTime = new TimeOnly(16, 0),
                        ShiftCount = 2,
                    },
                    // Add new
                    new ResourceTemplateRequest
                    {
                        ResourceTypeId = rt2.ResourceTypeId,
                        StartTime = new TimeOnly(10, 0),
                        EndTime = new TimeOnly(14, 0),
                        ShiftCount = 5,
                    }
                }
            };

            // Act
            var result = await service.UpdateEventTemplate(template.EventTemplateId, request);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.ResourceTemplates);
            Assert.Equal(2, result.ResourceTemplates.Count());

            // Verify in DB
            using var verifyContext = _fixture.CreateContext();
            var dbTemplate = await verifyContext.EventTemplates
                .Include(t => t.ResourceTemplates)
                .AsNoTracking()
                .SingleAsync(t => t.EventTemplateId == template.EventTemplateId);
            Assert.Equal(2, dbTemplate.ResourceTemplates.Count);
        }

        [Fact]
        public async Task UpdateEventTemplate_CanRemoveResourceTemplate()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var rt = await SeedResourceType(seedContext);

            var template = new EventTemplate
            {
                Name = UniqueName("RemRes"),
                EventName = UniqueName("RemResEvent"),
                StartTime = new DateTime(2026, 1, 15, 8, 0, 0),
                EndTime = new DateTime(2026, 1, 15, 16, 0, 0),
                ResourceTemplates = new List<ResourceTemplate>
                {
                    new ResourceTemplate
                    {
                        ResourceTypeId = rt.ResourceTypeId,
                        StartTime = new DateTime(2026, 1, 15, 8, 0, 0),
                        EndTime = new DateTime(2026, 1, 15, 16, 0, 0),
                        ShiftCount = 2,
                    }
                }
            };
            seedContext.EventTemplates.Add(template);
            await seedContext.SaveChangesAsync();
            var resourceTemplateId = template.ResourceTemplates.First().ResourceTemplateId;

            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            var request = new EventTemplateRequest
            {
                Name = template.Name,
                EventName = template.EventName,
                StartTime = new TimeOnly(8, 0),
                EndTime = new TimeOnly(16, 0),
                ResourceTemplates = new List<ResourceTemplateRequest>
                {
                    new ResourceTemplateRequest
                    {
                        Id = resourceTemplateId,
                        ResourceTypeId = rt.ResourceTypeId,
                        StartTime = new TimeOnly(8, 0),
                        EndTime = new TimeOnly(16, 0),
                        ShiftCount = 2,
                        IsDeleted = true,
                    }
                }
            };

            // Act
            var result = await service.UpdateEventTemplate(template.EventTemplateId, request);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.ResourceTemplates);
            Assert.Empty(result.ResourceTemplates);

            // Verify in DB
            using var verifyContext = _fixture.CreateContext();
            var dbResourceTemplate = await verifyContext.Set<ResourceTemplate>()
                .AsNoTracking()
                .SingleOrDefaultAsync(r => r.ResourceTemplateId == resourceTemplateId);
            Assert.Null(dbResourceTemplate);
        }

        [Fact]
        public async Task UpdateEventTemplate_ThrowsEntityNotFound_WhenNotFound()
        {
            // Arrange
            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            var request = new EventTemplateRequest
            {
                Name = "NonExistent",
                EventName = "NonExistent",
                StartTime = new TimeOnly(8, 0),
                EndTime = new TimeOnly(16, 0),
                ResourceTemplates = new List<ResourceTemplateRequest>()
            };

            // Act & Assert
            await Assert.ThrowsAsync<EntityNotFoundException>(() => service.UpdateEventTemplate(999999, request));
        }

        [Fact]
        public async Task CreateEventTemplate_SkipsDeletedResourceTemplates()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var rt = await SeedResourceType(seedContext);

            using var context = _fixture.CreateContext();
            var request = new EventTemplateRequest
            {
                Name = UniqueName("SkipDeleted"),
                EventName = UniqueName("SkipDeletedEvent"),
                StartTime = new TimeOnly(8, 0),
                EndTime = new TimeOnly(16, 0),
                ResourceTemplates = new List<ResourceTemplateRequest>
                {
                    new ResourceTemplateRequest { ResourceTypeId = rt.ResourceTypeId, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(15, 0), ShiftCount = 1 },
                    new ResourceTemplateRequest { ResourceTypeId = rt.ResourceTypeId, StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(14, 0), ShiftCount = 7, IsDeleted = true },
                },
            };

            // Act
            var result = await CreateService(context).CreateEventTemplate(request);

            // Assert
            using var verifyContext = _fixture.CreateContext();
            var dbTemplate = await verifyContext.EventTemplates.Include(t => t.ResourceTemplates).AsNoTracking()
                .SingleAsync(t => t.EventTemplateId == result.Id);
            Assert.Equal(1, Assert.Single(dbTemplate.ResourceTemplates).ShiftCount);
        }

        [Fact]
        public async Task CreateEventTemplate_StoresClockTimesOnReferenceDate()
        {
            // Arrange: bare klokkeslett, også over midnatt. Tidene lagres på den faste referansedatoen (2000-01-01),
            // som frontend sendte som dato hittil, og bare klokkeslettet brukes når malen tas i bruk.
            using var seedContext = _fixture.CreateContext();
            var rt = await SeedResourceType(seedContext);

            using var context = _fixture.CreateContext();
            var request = new EventTemplateRequest
            {
                Name = UniqueName("ReferenceDate"),
                EventName = UniqueName("ReferenceDateEvent"),
                StartTime = new TimeOnly(22, 0),
                EndTime = new TimeOnly(2, 0),
                ResourceTemplates = new List<ResourceTemplateRequest>
                {
                    new ResourceTemplateRequest { ResourceTypeId = rt.ResourceTypeId, StartTime = new TimeOnly(23, 15), EndTime = new TimeOnly(1, 30), ShiftCount = 1 },
                },
            };

            // Act
            var result = await CreateService(context).CreateEventTemplate(request);

            // Assert
            using var verifyContext = _fixture.CreateContext();
            var dbTemplate = await verifyContext.EventTemplates.Include(t => t.ResourceTemplates).AsNoTracking()
                .SingleAsync(t => t.EventTemplateId == result.Id);
            Assert.Equal(new DateTime(2000, 1, 1, 22, 0, 0), dbTemplate.StartTime);
            Assert.Equal(new DateTime(2000, 1, 1, 2, 0, 0), dbTemplate.EndTime);
            var dbResource = Assert.Single(dbTemplate.ResourceTemplates);
            Assert.Equal(new DateTime(2000, 1, 1, 23, 15, 0), dbResource.StartTime);
            Assert.Equal(new DateTime(2000, 1, 1, 1, 30, 0), dbResource.EndTime);
        }

        [Fact]
        public async Task DeleteEventTemplate_RemovesFromDatabase()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var rt = await SeedResourceType(seedContext);

            var template = new EventTemplate
            {
                Name = UniqueName("ToDelete"),
                EventName = UniqueName("ToDeleteEvent"),
                StartTime = new DateTime(2026, 1, 15, 8, 0, 0),
                EndTime = new DateTime(2026, 1, 15, 16, 0, 0),
            };
            seedContext.EventTemplates.Add(template);
            await seedContext.SaveChangesAsync();

            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act
            var result = await service.DeleteEventTemplate(template.EventTemplateId);

            // Assert
            Assert.NotNull(result);

            using var verifyContext = _fixture.CreateContext();
            var dbTemplate = await verifyContext.EventTemplates
                .AsNoTracking()
                .SingleOrDefaultAsync(t => t.EventTemplateId == template.EventTemplateId);
            Assert.Null(dbTemplate);
        }

        [Fact]
        public async Task DeleteEventTemplate_ThrowsEntityNotFound_WhenNotFound()
        {
            // Arrange
            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act & Assert
            await Assert.ThrowsAsync<EntityNotFoundException>(() => service.DeleteEventTemplate(999999));
        }

        #endregion

        #region CreateTemplateFromEvent

        [Fact]
        public async Task CreateTemplateFromEvent_CreatesTemplateFromExistingEvent()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var rt = await SeedResourceType(seedContext);
            var (evt, resource) = await SeedEventWithResource(seedContext, rt.ResourceTypeId);

            var templateName = UniqueName("FromEvent");

            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            var request = new TemplateFromEventRequest
            {
                Name = templateName,
            };

            // Act
            var result = await service.CreateTemplateFromEvent(evt.EventId, request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(templateName, result.Name);
            Assert.Equal(evt.Name, result.EventName);
            // Bare klokkeslettet tas med, på malens referansedato.
            Assert.Equal("2000-01-01T08:00", result.StartTime);
            Assert.Equal("2000-01-01T16:00", result.EndTime);
            Assert.NotNull(result.ResourceTemplates);
            Assert.Single(result.ResourceTemplates);
            var resTemplate = result.ResourceTemplates.First();
            Assert.Equal(rt.ResourceTypeId, resTemplate.ResourceType.Id);
            Assert.Equal(3, resTemplate.ShiftCount);
            Assert.Equal("2000-01-01T09:00", resTemplate.StartTime);
            Assert.Equal("2000-01-01T15:00", resTemplate.EndTime);

            // Verify in DB
            using var verifyContext = _fixture.CreateContext();
            var dbTemplate = await verifyContext.EventTemplates
                .Include(t => t.ResourceTemplates)
                .AsNoTracking()
                .SingleOrDefaultAsync(t => t.EventTemplateId == result.Id);
            Assert.NotNull(dbTemplate);
            Assert.Equal(templateName, dbTemplate.Name);
            Assert.Equal(evt.Name, dbTemplate.EventName);
            Assert.Single(dbTemplate.ResourceTemplates);
        }

        [Fact]
        public async Task CreateTemplateFromEvent_KeepsClockTimesOverMidnight_AndNewEventPlacesResourcesAsBefore()
        {
            // Arrange: vaktliste over midnatt med en vakt før og en etter midnatt.
            using var seedContext = _fixture.CreateContext();
            var rt = await SeedResourceType(seedContext);
            var evt = new Event
            {
                Name = UniqueName("NightEvent"),
                StartTime = new DateTime(2026, 3, 20, 22, 0, 0),
                EndTime = new DateTime(2026, 3, 21, 6, 0, 0),
                Resources = new List<EventResource>
                {
                    new EventResource { ResourceTypeId = rt.ResourceTypeId, StartTime = new DateTime(2026, 3, 20, 23, 0, 0), EndTime = new DateTime(2026, 3, 21, 1, 30, 0), ShiftCount = 1 },
                    new EventResource { ResourceTypeId = rt.ResourceTypeId, StartTime = new DateTime(2026, 3, 21, 2, 0, 0), EndTime = new DateTime(2026, 3, 21, 5, 0, 0), ShiftCount = 2 },
                }
            };
            seedContext.Events.Add(evt);
            await seedContext.SaveChangesAsync();

            using var context = _fixture.CreateContext();

            // Act
            var template = await CreateService(context).CreateTemplateFromEvent(evt.EventId, new TemplateFromEventRequest { Name = UniqueName("FromNightEvent") });

            // Assert: klokkeslettene beholdes, alle på referansedatoen.
            using var verifyContext = _fixture.CreateContext();
            var dbTemplate = await verifyContext.EventTemplates.Include(t => t.ResourceTemplates).AsNoTracking()
                .SingleAsync(t => t.EventTemplateId == template.Id);
            Assert.Equal(new DateTime(2000, 1, 1, 22, 0, 0), dbTemplate.StartTime);
            Assert.Equal(new DateTime(2000, 1, 1, 6, 0, 0), dbTemplate.EndTime);
            var resources = dbTemplate.ResourceTemplates.OrderBy(r => r.ShiftCount).ToList();
            Assert.Equal(new DateTime(2000, 1, 1, 23, 0, 0), resources[0].StartTime);
            Assert.Equal(new DateTime(2000, 1, 1, 1, 30, 0), resources[0].EndTime);
            Assert.Equal(new DateTime(2000, 1, 1, 2, 0, 0), resources[1].StartTime);
            Assert.Equal(new DateTime(2000, 1, 1, 5, 0, 0), resources[1].EndTime);

            // Ny vaktliste fra malen plasserer vaktene som i den opprinnelige vaktlista (bare klokkeslettet brukes).
            using var eventsContext = _fixture.CreateContext();
            var eventsService = new EventsService(eventsContext, new ResourceReader(eventsContext, TimeProvider.System), NSubstitute.Substitute.For<Middagsasen.Planner.Api.Authentication.ICurrentUserService>());
            var created = await eventsService.CreateEventFromTemplate(template.Id, new EventFromTemplateRequest { StartDate = new DateOnly(2026, 4, 10) });

            using var eventVerifyContext = _fixture.CreateContext();
            var dbEvent = await eventVerifyContext.Events.Include(e => e.Resources).AsNoTracking().SingleAsync(e => e.EventId == created.Id);
            Assert.Equal(new DateTime(2026, 4, 10, 22, 0, 0), dbEvent.StartTime);
            Assert.Equal(new DateTime(2026, 4, 11, 6, 0, 0), dbEvent.EndTime);
            var eventResources = dbEvent.Resources.OrderBy(r => r.ShiftCount).ToList();
            Assert.Equal(new DateTime(2026, 4, 10, 23, 0, 0), eventResources[0].StartTime);
            Assert.Equal(new DateTime(2026, 4, 11, 1, 30, 0), eventResources[0].EndTime);
            Assert.Equal(new DateTime(2026, 4, 11, 2, 0, 0), eventResources[1].StartTime);
            Assert.Equal(new DateTime(2026, 4, 11, 5, 0, 0), eventResources[1].EndTime);
        }

        [Fact]
        public async Task CreateTemplateFromEvent_ThrowsEntityNotFound_WhenEventNotFound()
        {
            // Arrange
            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            var request = new TemplateFromEventRequest
            {
                Name = "NonExistent",
            };

            // Act & Assert
            await Assert.ThrowsAsync<EntityNotFoundException>(() => service.CreateTemplateFromEvent(999999, request));
        }

        #endregion

        #region ResourceType i malsvar (#115)

        [Fact]
        public async Task GetEventTemplateById_ResourceTypeWithTrainersAndFiles_HasTrainingTrainersAndFiles()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var trainer = new User { UserName = UniqueName("user"), FirstName = "Trener", LastName = "Mal", Created = DateTime.UtcNow };
            seedContext.Users.Add(trainer);
            await seedContext.SaveChangesAsync();

            var rt = new ResourceType { Name = UniqueName("RT"), DefaultShiftCount = 2 };
            rt.Trainers.Add(new ResourceTypeTrainer { UserId = trainer.UserId });
            rt.Files.Add(new ResourceTypeFile
            {
                StorageName = Guid.NewGuid().ToString(),
                FileName = "instruks.pdf",
                Description = "Instruks",
                MimeType = "application/pdf",
                Created = new DateTime(2026, 1, 10, 8, 30, 0),
                CreatedBy = trainer.UserId,
                Updated = new DateTime(2026, 1, 10, 8, 30, 0),
                UpdatedBy = trainer.UserId,
            });
            var template = new EventTemplate
            {
                Name = UniqueName("Mal"),
                EventName = UniqueName("Event"),
                StartTime = new DateTime(2026, 1, 15, 8, 0, 0),
                EndTime = new DateTime(2026, 1, 15, 16, 0, 0),
                ResourceTemplates =
                [
                    new ResourceTemplate { ResourceType = rt, StartTime = new DateTime(2026, 1, 15, 9, 0, 0), EndTime = new DateTime(2026, 1, 15, 15, 0, 0), ShiftCount = 2 },
                ],
            };
            seedContext.EventTemplates.Add(template);
            await seedContext.SaveChangesAsync();

            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act
            var byId = await service.GetEventTemplateById(template.EventTemplateId);
            var fromList = (await service.GetEventTemplates()).Single(t => t.Id == template.EventTemplateId);

            // Assert
            foreach (var result in new[] { byId, fromList })
            {
                var resourceType = Assert.Single(result.ResourceTemplates!).ResourceType;
                Assert.True(resourceType.HasTraining);
                var resourceTypeTrainer = Assert.Single(resourceType.Trainers);
                Assert.Equal(trainer.UserId, resourceTypeTrainer.UserId);
                Assert.Equal("Trener Mal", resourceTypeTrainer.FullName);
                var file = Assert.Single(resourceType.Files);
                Assert.Equal("instruks.pdf", file.FileName);
                Assert.Equal("Trener Mal", file.CreatedBy);
            }
        }

        [Fact]
        public async Task DeleteEventTemplate_ReturnsDeletedTemplateWithResourceTemplates()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var rt = await SeedResourceType(seedContext);
            var template = new EventTemplate
            {
                Name = UniqueName("Slett"),
                EventName = UniqueName("Event"),
                StartTime = new DateTime(2026, 1, 15, 8, 0, 0),
                EndTime = new DateTime(2026, 1, 15, 16, 0, 0),
                ResourceTemplates =
                [
                    new ResourceTemplate { ResourceTypeId = rt.ResourceTypeId, StartTime = new DateTime(2026, 1, 15, 9, 0, 0), EndTime = new DateTime(2026, 1, 15, 15, 0, 0), ShiftCount = 2 },
                ],
            };
            seedContext.EventTemplates.Add(template);
            await seedContext.SaveChangesAsync();

            using var context = _fixture.CreateContext();

            // Act
            var result = await CreateService(context).DeleteEventTemplate(template.EventTemplateId);

            // Assert
            Assert.Equal(template.EventTemplateId, result.Id);
            Assert.Equal(rt.ResourceTypeId, Assert.Single(result.ResourceTemplates!).ResourceType.Id);
            using var verifyContext = _fixture.CreateContext();
            Assert.False(await verifyContext.EventTemplates.AnyAsync(t => t.EventTemplateId == template.EventTemplateId));
        }

        #endregion
    }
}
