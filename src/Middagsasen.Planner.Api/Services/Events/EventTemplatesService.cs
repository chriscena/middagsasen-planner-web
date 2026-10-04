using Microsoft.EntityFrameworkCore;
using Middagsasen.Planner.Api.Core;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services.Resources;
using Middagsasen.Planner.Api.Services.ResourceTypes;

namespace Middagsasen.Planner.Api.Services.Events
{
    public class EventTemplatesService : IEventTemplatesService
    {
        /// <summary>
        /// Malene bruker bare klokkeslettet (se <see cref="EventsService.CreateEventFromTemplate"/>), men tidene lagres
        /// som <see cref="DateTime"/>. Nye og endrede tider lagres på denne faste datoen, som frontend også har sendt hittil.
        /// </summary>
        internal static readonly DateOnly TemplateReferenceDate = new(2000, 1, 1);

        public EventTemplatesService(PlannerDbContext dbContext, IResourceReader reader)
        {
            DbContext = dbContext;
            Reader = reader;
        }

        public PlannerDbContext DbContext { get; }
        public IResourceReader Reader { get; }

        private IQueryable<EventTemplate> EventTemplates => DbContext.EventTemplates
                .Include(e => e.ResourceTemplates);

        public async Task<IEnumerable<EventTemplateResponse>> GetEventTemplates()
        {
            var templates = await EventTemplates
                .AsNoTracking()
                .ToListAsync();
            return await Map(templates);
        }

        public async Task<EventTemplateResponse> GetEventTemplateById(int id)
        {
            var template = await EventTemplates
                .AsNoTracking()
                .SingleOrDefaultAsync(e => e.EventTemplateId == id)
                ?? throw new EntityNotFoundException();
            return (await Map([template])).Single();
        }

        public async Task<EventTemplateResponse> CreateEventTemplate(EventTemplateRequest request)
        {
            var newEvent = new EventTemplate
            {
                Name = request.Name,
                EventName = request.EventName,
                StartTime = ToTemplateTime(request.StartTime),
                EndTime = ToTemplateTime(request.EndTime),
                ResourceTemplates = request.ResourceTemplates.Select(Map).ToList(),
            };

            DbContext.EventTemplates.Add(newEvent);
            await DbContext.SaveChangesAsync();

            return await GetEventTemplateById(newEvent.EventTemplateId);
        }

        public async Task<EventTemplateResponse> UpdateEventTemplate(int id, EventTemplateRequest request)
        {
            var existingEvent = await EventTemplates
            .SingleOrDefaultAsync(e => e.EventTemplateId == id)
            ?? throw new EntityNotFoundException();

            existingEvent.Name = request.Name;
            existingEvent.EventName = request.EventName;
            existingEvent.StartTime = ToTemplateTime(request.StartTime);
            existingEvent.EndTime = ToTemplateTime(request.EndTime);

            foreach (var resource in request.ResourceTemplates)
            {
                if (resource.IsDeleted)
                {
                    var resourceToDelete = existingEvent.ResourceTemplates.FirstOrDefault(r => r.ResourceTemplateId == resource.Id);
                    if (resourceToDelete == null) continue;
                    existingEvent.ResourceTemplates.Remove(resourceToDelete);
                }
                else if (!resource.Id.HasValue)
                {
                    existingEvent.ResourceTemplates.Add(Map(resource));
                }
                else
                {
                    var resourceToUpdate = existingEvent.ResourceTemplates.FirstOrDefault(r => r.ResourceTemplateId == resource.Id);
                    if (resourceToUpdate == null) continue;
                    resourceToUpdate.ResourceTypeId = resource.ResourceTypeId;
                    resourceToUpdate.StartTime = ToTemplateTime(resource.StartTime);
                    resourceToUpdate.EndTime = ToTemplateTime(resource.EndTime);
                    resourceToUpdate.MinimumStaff = resource.MinimumStaff;
                }
            }
            await DbContext.SaveChangesAsync();

            return await GetEventTemplateById(id);
        }

        public async Task<EventTemplateResponse> DeleteEventTemplate(int id)
        {
            var existingTemplate = await EventTemplates.SingleOrDefaultAsync(e => e.EventTemplateId == id)
                ?? throw new EntityNotFoundException();

            // Svaret bygges før slettingen, så det inneholder den slettede malen med ressursmalene.
            var response = (await Map([existingTemplate])).Single();

            DbContext.EventTemplates.Remove(existingTemplate);
            await DbContext.SaveChangesAsync();

            return response;
        }

        public async Task<EventTemplateResponse> CreateTemplateFromEvent(int id, TemplateFromEventRequest request)
        {
            var existingEvent = await DbContext.Events
                .Include(e => e.Resources)
                .AsNoTracking()
                .SingleOrDefaultAsync(e => e.EventId == id)
                ?? throw new EntityNotFoundException();

            var template = new EventTemplate
            {
                Name = request.Name,
                EventName = existingEvent.Name,
                StartTime = existingEvent.StartTime,
                EndTime = existingEvent.EndTime,
                ResourceTemplates = existingEvent.Resources.Select(r => new ResourceTemplate
                {
                    ResourceTypeId = r.ResourceTypeId,
                    StartTime = r.StartTime,
                    EndTime = r.EndTime,
                    MinimumStaff = r.MinimumStaff,
                }).ToList(),
            };

            DbContext.EventTemplates.Add(template);
            await DbContext.SaveChangesAsync();

            return await GetEventTemplateById(template.EventTemplateId);
        }

        /// <summary>
        /// Mapper malene. Ressurstypene hentes fra lesemodulen for ressurser (også inaktive), så de er like som ellers i API-et.
        /// </summary>
        private async Task<List<EventTemplateResponse>> Map(IReadOnlyCollection<EventTemplate> templates)
        {
            var resourceTypes = await Reader.GetResourceTypes(
                templates.SelectMany(t => t.ResourceTemplates).Select(r => r.ResourceTypeId));

            return templates.Select(template => new EventTemplateResponse
            {
                Id = template.EventTemplateId,
                Name = template.Name,
                EventName = template.EventName,
                StartTime = template.StartTime.ToSimpleIsoString(),
                EndTime = template.EndTime.ToSimpleIsoString(),
                ResourceTemplates = template.ResourceTemplates
                    .Select(r => Map(r, resourceTypes[r.ResourceTypeId]))
                    .ToList(),
            }).ToList();
        }

        private static ResourceTemplateResponse Map(ResourceTemplate template, ResourceTypeResponse resourceType) => new()
        {
            Id = template.ResourceTemplateId,
            ResourceType = resourceType,
            StartTime = template.StartTime.ToSimpleIsoString(),
            EndTime = template.EndTime.ToSimpleIsoString(),
            MinimumStaff = template.MinimumStaff,
        };

        private static DateTime ToTemplateTime(TimeOnly time) => TemplateReferenceDate.ToDateTime(time);

        /// <summary>Ny ressursmal. Fremmednøkkelen til malen settes av EF via navigasjonen ved lagring.</summary>
        private static ResourceTemplate Map(ResourceTemplateRequest resource) => new()
        {
            ResourceTypeId = resource.ResourceTypeId,
            StartTime = ToTemplateTime(resource.StartTime),
            EndTime = ToTemplateTime(resource.EndTime),
            MinimumStaff = resource.MinimumStaff,
        };
    }
}
