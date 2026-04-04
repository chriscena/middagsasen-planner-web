using Microsoft.EntityFrameworkCore;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services.Events;
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
            return new EventTemplatesService(context);
        }

        private static string UniqueName(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

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
                        MinimumStaff = 3,
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
                StartTime = "2026-01-15T08:00:00",
                EndTime = "2026-01-15T16:00:00",
                ResourceTemplates = new List<ResourceTemplateRequest>
                {
                    new ResourceTemplateRequest
                    {
                        ResourceTypeId = rt.ResourceTypeId,
                        StartTime = "2026-01-15T09:00:00",
                        EndTime = "2026-01-15T15:00:00",
                        MinimumStaff = 3,
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
            Assert.Equal(3, dbTemplate.ResourceTemplates.First().MinimumStaff);
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
                    new ResourceTemplate { ResourceTypeId = rt.ResourceTypeId, StartTime = new DateTime(2026, 1, 15, 8, 0, 0), EndTime = new DateTime(2026, 1, 15, 16, 0, 0), MinimumStaff = 1 }
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
                    new ResourceTemplate { ResourceTypeId = rt.ResourceTypeId, StartTime = new DateTime(2026, 2, 15, 8, 0, 0), EndTime = new DateTime(2026, 2, 15, 16, 0, 0), MinimumStaff = 1 }
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
                        MinimumStaff = 4,
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
            Assert.Equal(4, resourceTemplate.MinimumStaff);
        }

        [Fact]
        public async Task GetEventTemplateById_ReturnsNull_WhenNotFound()
        {
            // Arrange
            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act
            var result = await service.GetEventTemplateById(999999);

            // Assert
            Assert.Null(result);
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
                        MinimumStaff = 2,
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
                StartTime = "2026-02-20T10:00:00",
                EndTime = "2026-02-20T18:00:00",
                ResourceTemplates = new List<ResourceTemplateRequest>
                {
                    new ResourceTemplateRequest
                    {
                        Id = resourceTemplateId,
                        ResourceTypeId = rt.ResourceTypeId,
                        StartTime = "2026-02-20T10:00:00",
                        EndTime = "2026-02-20T18:00:00",
                        MinimumStaff = 2,
                    }
                }
            };

            // Act
            var result = await service.UpdateEventTemplate(template.EventTemplateId, request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(updatedName, result.Name);
            Assert.Equal(updatedEventName, result.EventName);
            Assert.Equal("2026-02-20T10:00", result.StartTime);
            Assert.Equal("2026-02-20T18:00", result.EndTime);

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
                        MinimumStaff = 2,
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
                StartTime = "2026-01-15T08:00:00",
                EndTime = "2026-01-15T16:00:00",
                ResourceTemplates = new List<ResourceTemplateRequest>
                {
                    // Keep existing
                    new ResourceTemplateRequest
                    {
                        Id = existingResourceTemplateId,
                        ResourceTypeId = rt1.ResourceTypeId,
                        StartTime = "2026-01-15T08:00:00",
                        EndTime = "2026-01-15T16:00:00",
                        MinimumStaff = 2,
                    },
                    // Add new
                    new ResourceTemplateRequest
                    {
                        ResourceTypeId = rt2.ResourceTypeId,
                        StartTime = "2026-01-15T10:00:00",
                        EndTime = "2026-01-15T14:00:00",
                        MinimumStaff = 5,
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
                        MinimumStaff = 2,
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
                StartTime = "2026-01-15T08:00:00",
                EndTime = "2026-01-15T16:00:00",
                ResourceTemplates = new List<ResourceTemplateRequest>
                {
                    new ResourceTemplateRequest
                    {
                        Id = resourceTemplateId,
                        ResourceTypeId = rt.ResourceTypeId,
                        StartTime = "2026-01-15T08:00:00",
                        EndTime = "2026-01-15T16:00:00",
                        MinimumStaff = 2,
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
        public async Task UpdateEventTemplate_ReturnsNull_WhenNotFound()
        {
            // Arrange
            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            var request = new EventTemplateRequest
            {
                Name = "NonExistent",
                EventName = "NonExistent",
                StartTime = "2026-01-15T08:00:00",
                EndTime = "2026-01-15T16:00:00",
                ResourceTemplates = new List<ResourceTemplateRequest>()
            };

            // Act
            var result = await service.UpdateEventTemplate(999999, request);

            // Assert
            Assert.Null(result);
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
        public async Task DeleteEventTemplate_ReturnsNull_WhenNotFound()
        {
            // Arrange
            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act
            var result = await service.DeleteEventTemplate(999999);

            // Assert
            Assert.Null(result);
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
            Assert.Equal("2026-01-15T08:00", result.StartTime);
            Assert.Equal("2026-01-15T16:00", result.EndTime);
            Assert.NotNull(result.ResourceTemplates);
            Assert.Single(result.ResourceTemplates);
            var resTemplate = result.ResourceTemplates.First();
            Assert.Equal(rt.ResourceTypeId, resTemplate.ResourceType.Id);
            Assert.Equal(3, resTemplate.MinimumStaff);
            Assert.Equal("2026-01-15T09:00", resTemplate.StartTime);
            Assert.Equal("2026-01-15T15:00", resTemplate.EndTime);

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
        public async Task CreateTemplateFromEvent_ReturnsNull_WhenEventNotFound()
        {
            // Arrange
            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            var request = new TemplateFromEventRequest
            {
                Name = "NonExistent",
            };

            // Act
            var result = await service.CreateTemplateFromEvent(999999, request);

            // Assert
            Assert.Null(result);
        }

        #endregion
    }
}
