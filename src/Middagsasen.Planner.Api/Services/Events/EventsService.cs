using Microsoft.EntityFrameworkCore;
using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Core;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services.Shifts;

namespace Middagsasen.Planner.Api.Services.Events
{
    public class EventsService : IEventsService
    {
        internal const string MessageEmptyMessage = "Beskjeden kan ikke være tom.";
        internal static readonly string MessageTooLongMessage = $"Beskjeden kan ikke være lengre enn {MessageRequest.MaxLength} tegn.";

        public EventsService(PlannerDbContext dbContext, IShiftService shiftService, ICurrentUserService currentUser)
        {
            DbContext = dbContext;
            ShiftService = shiftService;
            CurrentUser = currentUser;
        }

        public PlannerDbContext DbContext { get; }
        public IShiftService ShiftService { get; }
        public ICurrentUserService CurrentUser { get; }

        public async Task<IEnumerable<EventStatusResponse>> GetEventStatuses(int month, int year)
        {
            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1);
            var eventStatuses = await DbContext.EventStatuses.Where(e => e.StartTime >= startDate && e.StartTime < endDate).ToListAsync();

            var response = eventStatuses.Select(e => new
            {
                Date = e.StartTime.ToString("yyyy'/'MM'/'dd"),
                IsMissingStaff = e.MissingStaff == 1,
            })
                .GroupBy(r => r.Date)
                .Select(g => new EventStatusResponse
                {
                    Date = g.Key,
                    IsMissingStaff = g.Any(e => e.IsMissingStaff),
                });
            return response;
        }

        /// <summary>
        /// Events med alt <see cref="ResourceMapper"/> trenger for ressursene (samme som <see cref="ShiftRepository.WithMappingIncludes"/>).
        /// </summary>
        private IQueryable<Event> Events => DbContext.Events
                .Include(e => e.Resources)
                    .ThenInclude(r => r.Shifts)
                        .ThenInclude(s => s.User)
                            .ThenInclude(u =>u.Trainings)
                .Include(e => e.Resources)
                    .ThenInclude(r => r.Shifts)
                        .ThenInclude(s => s.User)
                            .ThenInclude(u => u.Competencies)
                .Include(e => e.Resources)
                    .ThenInclude(r => r.ResourceType)
                        .ThenInclude(rt => rt.Trainers)
                            .ThenInclude(t => t.User)
                .Include(e => e.Resources)
                    .ThenInclude(r => r.ResourceType)
                        .ThenInclude(rt => rt.Files)
                .Include(e => e.Resources)
                    .ThenInclude(r => r.ResourceType)
                        .ThenInclude(rt => rt.RequiredCompetencies)
                            .ThenInclude(rc => rc.Competency)
                .Include(e => e.Resources)
                    .ThenInclude(r => r.Messages)
                        .ThenInclude(t => t.CreatedByUser);

        public async Task<IEnumerable<EventResponse>> GetEvents()
        {
            var events = await Events
                .AsNoTracking()
                .AsSplitQuery()
                .ToListAsync();

            var mapper = await ShiftService.CreateResourceMapper();
            return events.Select(mapper.Map).ToList();
        }

        public async Task<IEnumerable<EventResponse>> GetEvents(DateTime start, DateTime end)
        {
            var events = await Events
                .AsNoTracking()
                .Where(e => e.StartTime >= start && e.StartTime < end)
                .AsSplitQuery()
                .ToListAsync();

            var mapper = await ShiftService.CreateResourceMapper();
            return events.Select(mapper.Map).ToList();
        }

        public async Task<EventResponse> GetEventById(int id)
        {
            var existingEvent = await Events
                .AsNoTracking()
                .AsSplitQuery()
                .SingleOrDefaultAsync(e => e.EventId == id)
                ?? throw new EntityNotFoundException("Kunne ikke finne vakt.");

            var mapper = await ShiftService.CreateResourceMapper();
            return mapper.Map(existingEvent);
        }

        public async Task<IEnumerable<ShiftSeasonResponse>> GetShiftsByUserId(int id)
        {
            var shifts = await DbContext.Shifts
                .Include(e => e.Resource)
                    .ThenInclude(e => e.ResourceType)
                .AsNoTracking()
                .Where(s => s.UserId == id)
                .ToListAsync();

            var response = shifts
                .Select(s => new UserShiftResponse
                {
                    Id = s.EventResourceUserId,
                    StartDate = s.StartTime?.ToString("yyyy'-'MM'-'dd"),
                    StartTime = s.StartTime.ToSimpleIsoString(),
                    EndTime = s.EndTime.ToSimpleIsoString(),
                    ResourceName = s.Resource.ResourceType.Name,
                    Season = s.StartTime.ToSeason(),
                    Comment = s.Comment,
                })
                .OrderByDescending(s => s.StartTime)
                .GroupBy(s => s.Season)
                .Select(s => new ShiftSeasonResponse { Label = s.Key, Shifts = [..s] })
                .ToList();

            return response;
        }

        public async Task<EventResponse> CreateEvent(EventRequest request)
        {
            var eventStart = DateTime.Parse(request.StartTime);
            var eventEnd = ResourceTimes.NormalizeEventEnd(eventStart, DateTime.Parse(request.EndTime));
            var newEvent = new Event
            {
                Name = request.Name,
                Description = request.Description,
                StartTime = eventStart,
                EndTime = eventEnd,
                Resources = request.Resources.Select(r => Map(r, eventStart, eventEnd)).ToList(),
            };

            DbContext.Events.Add(newEvent);
            await DbContext.SaveChangesAsync();

            return await GetEventById(newEvent.EventId);
        }

        public async Task<EventResponse> UpdateEvent(int eventId, EventRequest request)
        {
            var existingEvent = await Events
            .SingleOrDefaultAsync(e => e.EventId == eventId)
            ?? throw new EntityNotFoundException();

            existingEvent.Name = request.Name;
            existingEvent.Description = request.Description;
            var eventStart = DateTime.Parse(request.StartTime);
            var eventEnd = ResourceTimes.NormalizeEventEnd(eventStart, DateTime.Parse(request.EndTime));
            existingEvent.StartTime = eventStart;
            existingEvent.EndTime = eventEnd;

            foreach (var resource in request.Resources)
            {
                if (resource.IsDeleted)
                {
                    var resourceToDelete = existingEvent.Resources.FirstOrDefault(r => r.EventResourceId == resource.Id);
                    if (resourceToDelete == null) continue;
                    existingEvent.Resources.Remove(resourceToDelete);
                }
                else if (!resource.Id.HasValue)
                {
                    existingEvent.Resources.Add(Map(resource, eventStart, eventEnd));
                }
                else
                {
                    var resourceToUpdate = existingEvent.Resources.FirstOrDefault(r => r.EventResourceId == resource.Id);
                    if (resourceToUpdate == null) continue;
                    resourceToUpdate.ResourceTypeId = resource.ResourceTypeId;
                    (resourceToUpdate.StartTime, resourceToUpdate.EndTime) = PlaceResource(resource, eventStart, eventEnd);
                    resourceToUpdate.MinimumStaff = resource.MinimumStaff;
                }
            }
            await DbContext.SaveChangesAsync();

            return await GetEventById(eventId);
        }

        public async Task<EventResponse> DeleteEvent(int id)
        {
            var existingEvent = await DbContext.Events.SingleOrDefaultAsync(e => e.EventId == id)
                ?? throw new EntityNotFoundException();

            DbContext.Events.Remove(existingEvent);

            await DbContext.SaveChangesAsync();

            var mapper = await ShiftService.CreateResourceMapper();
            return mapper.Map(existingEvent);
        }

        private async Task EnsureEventResourceExists(int eventResourceId)
        {
            if (!await DbContext.EventResource.AnyAsync(er => er.EventResourceId == eventResourceId))
                throw new EntityNotFoundException("Fant ikke vaktressursen.");
        }

        public async Task<EventResponse> CreateEventFromTemplate(int templateId, EventFromTemplateRequest request)
        {
            var startDate = DateTime.Parse(request.StartDate);

            var template = await DbContext.EventTemplates
                .Include(e => e.ResourceTemplates)
                .AsNoTracking()
                .SingleOrDefaultAsync(e => e.EventTemplateId == templateId)
                ?? throw new EntityNotFoundException();

            var startTime = startDate.Date + template.StartTime.TimeOfDay;
            var endTime = ResourceTimes.NormalizeEventEnd(startTime, startDate.Date + template.EndTime.TimeOfDay);

            var newEvent = new Event
            {
                Name = template.EventName,
                StartTime = startTime,
                EndTime = endTime,
                Resources = template.ResourceTemplates.Select(r =>
                {
                    var (resourceStartTime, resourceEndTime) = ResourceTimes.Place(startTime, endTime, r.StartTime.TimeOfDay, r.EndTime.TimeOfDay);
                    return new EventResource
                    {
                        ResourceTypeId = r.ResourceTypeId,
                        StartTime = resourceStartTime,
                        EndTime = resourceEndTime,
                        MinimumStaff = r.MinimumStaff,
                    };
                }).ToList(),
            };

            DbContext.Events.Add(newEvent);
            await DbContext.SaveChangesAsync();

            return await GetEventById(newEvent.EventId);
        }

        /// <summary>
        /// Bruker kun klokkeslettet fra innsendte ressurstider; døgnet bestemmes av <see cref="ResourceTimes.Place"/>.
        /// </summary>
        private static (DateTime Start, DateTime End) PlaceResource(ResourceRequest request, DateTime eventStart, DateTime eventEnd) =>
            ResourceTimes.Place(eventStart, eventEnd, DateTime.Parse(request.StartTime).TimeOfDay, DateTime.Parse(request.EndTime).TimeOfDay);

        private EventResource Map(ResourceRequest request, DateTime eventStart, DateTime eventEnd)
        {
            var (start, end) = PlaceResource(request, eventStart, eventEnd);
            var resource = new EventResource
            {
                ResourceTypeId = request.ResourceTypeId,
                StartTime = start,
                EndTime = end,
                MinimumStaff = request.MinimumStaff,
            };
            if (request.Id.HasValue)
            {
                resource.EventResourceId = request.Id.Value;
            }
            return resource;
        }

        public async Task<IEnumerable<MessageResponse>> GetMessages(int eventResourceId)
        {
            var messages = await DbContext.Messages
                .Include(m => m.CreatedByUser)
                .AsNoTracking()
                .Where(m => m.EventResourceId == eventResourceId)
                .ToListAsync();

            return messages.Select(ResourceMapper.MapMessage).ToList();
        }

        public async Task<MessageResponse> AddMessage(int eventResourceId, int createdBy, MessageRequest request)
        {
            // Valideres i servicen også, slik at ugyldig input avvises uavhengig av MVC-modellvalidering.
            if (string.IsNullOrWhiteSpace(request.Message))
                throw new DomainValidationException(MessageEmptyMessage);

            var text = request.Message.Trim();
            if (text.Length > MessageRequest.MaxLength)
                throw new DomainValidationException(MessageTooLongMessage);

            await EnsureEventResourceExists(eventResourceId);

            var message = new EventResourceMessage
            {
                Message = text,
                EventResourceId = eventResourceId,
                Created = DateTime.UtcNow,
                CreatedBy = createdBy,
            };
            DbContext.Messages.Add(message);
            await DbContext.SaveChangesAsync();

            var response = await DbContext.Messages
                .Include(m => m.CreatedByUser)
                .AsNoTracking()
                .SingleAsync(m => m.EventResourceMessageId == message.EventResourceMessageId);
            return ResourceMapper.MapMessage(response);
        }

        public async Task<MessageResponse> DeleteMessage(int id, int eventResourceId)
        {
            var message = await DbContext.Messages
                .Include(m => m.CreatedByUser)
                .SingleOrDefaultAsync(m => m.EventResourceId == eventResourceId && m.EventResourceMessageId == id)
                ?? throw new EntityNotFoundException();

            if (!MessagePolicy.CanDelete(CurrentUser.ToActor(), message))
                throw new ForbiddenAccessException();

            DbContext.Remove(message);
            await DbContext.SaveChangesAsync();

            return ResourceMapper.MapMessage(message);
        }

        public async Task<MinimumStaffResponse> UpdateMinimumStaff(int id, MinimumStaffRequest request)
        {
            var eventResource = await DbContext.EventResource
                .SingleOrDefaultAsync(er => er.EventResourceId == id)
                ?? throw new EntityNotFoundException();

            eventResource.MinimumStaff = request.MinimumStaff;
            await DbContext.SaveChangesAsync();

            return new MinimumStaffResponse
            {
                EventResourceId = eventResource.EventResourceId,
                MinimumStaff = eventResource.MinimumStaff
            };
        }
    }
}
