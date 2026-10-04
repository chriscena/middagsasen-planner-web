using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Core;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services.Resources;

namespace Middagsasen.Planner.Api.Services.Events
{
    public class EventsService : IEventsService
    {
        internal const string MessageEmptyMessage = "Beskjeden kan ikke være tom.";
        internal static readonly string MessageTooLongMessage = $"Beskjeden kan ikke være lengre enn {MessageRequest.MaxLength} tegn.";

        internal const string EventNotFoundMessage = "Fant ikke arrangementet.";
        internal const string InvalidStartDateMessage = "Ugyldig startdato. Bruk formatet ÅÅÅÅ-MM-DD.";
        internal const string InvalidStartTimeMessage = "Ugyldig starttid.";
        internal const string InvalidEndTimeMessage = "Ugyldig sluttid.";
        internal const string InvalidResourceStartTimeMessage = "Ugyldig starttid for vakt.";
        internal const string InvalidResourceEndTimeMessage = "Ugyldig sluttid for vakt.";

        public EventsService(PlannerDbContext dbContext, IResourceReader reader, ICurrentUserService currentUser)
        {
            DbContext = dbContext;
            Reader = reader;
            CurrentUser = currentUser;
        }

        public PlannerDbContext DbContext { get; }
        public IResourceReader Reader { get; }
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

        public async Task<IEnumerable<EventResponse>> GetEvents()
        {
            return await Reader.GetEvents(CurrentUser.ToActor());
        }

        public async Task<IEnumerable<EventResponse>> GetEvents(DateTime start, DateTime end)
        {
            return await Reader.GetEvents(CurrentUser.ToActor(), start, end);
        }

        public async Task<EventResponse> GetEventById(int id)
        {
            return await Reader.GetEvent(CurrentUser.ToActor(), id)
                ?? throw new EntityNotFoundException(EventNotFoundMessage);
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
            var (eventStart, eventEnd) = ParseEventTimes(request);
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
            // Alle tider valideres før den sporede entiteten endres, så en valideringsfeil ikke etterlater
            // en halvveis endret entitet. Slettede ressurser brukes ikke, og tidene deres valideres derfor ikke.
            var (eventStart, eventEnd) = ParseEventTimes(request);
            var resources = request.Resources
                .Select(r => (Request: r, Times: r.IsDeleted ? default : PlaceResource(r, eventStart, eventEnd)))
                .ToList();

            var existingEvent = await DbContext.Events
                .Include(e => e.Resources)
                .SingleOrDefaultAsync(e => e.EventId == eventId)
                ?? throw new EntityNotFoundException(EventNotFoundMessage);

            existingEvent.Name = request.Name;
            existingEvent.Description = request.Description;
            existingEvent.StartTime = eventStart;
            existingEvent.EndTime = eventEnd;

            foreach (var (resource, times) in resources)
            {
                if (resource.IsDeleted)
                {
                    var resourceToDelete = existingEvent.Resources.FirstOrDefault(r => r.EventResourceId == resource.Id);
                    if (resourceToDelete == null) continue;
                    existingEvent.Resources.Remove(resourceToDelete);
                }
                else if (!resource.Id.HasValue)
                {
                    existingEvent.Resources.Add(Map(resource, times));
                }
                else
                {
                    var resourceToUpdate = existingEvent.Resources.FirstOrDefault(r => r.EventResourceId == resource.Id);
                    if (resourceToUpdate == null) continue;
                    resourceToUpdate.ResourceTypeId = resource.ResourceTypeId;
                    (resourceToUpdate.StartTime, resourceToUpdate.EndTime) = times;
                    resourceToUpdate.MinimumStaff = resource.MinimumStaff;
                }
            }
            await DbContext.SaveChangesAsync();

            return await GetEventById(eventId);
        }

        public async Task<EventResponse> DeleteEvent(int id)
        {
            // Svaret leses før slettingen, så det inneholder det slettede arrangementet med ressursene.
            var response = await Reader.GetEvent(CurrentUser.ToActor(), id)
                ?? throw new EntityNotFoundException(EventNotFoundMessage);

            // Kan være slettet av en samtidig forespørsel siden svaret ble lest.
            var existingEvent = await DbContext.Events.SingleOrDefaultAsync(e => e.EventId == id)
                ?? throw new EntityNotFoundException(EventNotFoundMessage);
            DbContext.Events.Remove(existingEvent);
            await DbContext.SaveChangesAsync();

            return response;
        }

        private async Task EnsureEventResourceExists(int eventResourceId)
        {
            if (!await DbContext.EventResource.AnyAsync(er => er.EventResourceId == eventResourceId))
                throw new EntityNotFoundException("Fant ikke vaktressursen.");
        }

        public async Task<EventResponse> CreateEventFromTemplate(int templateId, EventFromTemplateRequest request)
        {
            // Frontend sender dagnøkkel (yyyy-MM-dd). Ugyldig dato er en valideringsfeil (400), ikke en intern feil.
            if (!DateOnly.TryParseExact(request.StartDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var startDate))
                throw new DomainValidationException(InvalidStartDateMessage);
            var startDay = startDate.ToDateTime(TimeOnly.MinValue);

            var template = await DbContext.EventTemplates
                .Include(e => e.ResourceTemplates)
                .AsNoTracking()
                .SingleOrDefaultAsync(e => e.EventTemplateId == templateId)
                ?? throw new EntityNotFoundException();

            var startTime = startDay + template.StartTime.TimeOfDay;
            var endTime = ResourceTimes.NormalizeEventEnd(startTime, startDay + template.EndTime.TimeOfDay);

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
        /// Tolker en innsendt tid. Frontend sender lokal tid uten sone (<c>yyyy-MM-ddTHH:mm</c>); ISO 8601
        /// tolkes likt uavhengig av kultur, og <see cref="DateTimeStyles.None"/> gir samme resultat (inkl.
        /// <see cref="DateTime.Kind"/>) som <see cref="DateTime.Parse(string)"/>. Ugyldig eller tom verdi gir 400.
        /// </summary>
        private static DateTime ParseDateTime(string? value, string message) =>
            DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result)
                ? result
                : throw new DomainValidationException(message);

        private static (DateTime Start, DateTime End) ParseEventTimes(EventRequest request)
        {
            var start = ParseDateTime(request.StartTime, InvalidStartTimeMessage);
            var end = ParseDateTime(request.EndTime, InvalidEndTimeMessage);
            return (start, ResourceTimes.NormalizeEventEnd(start, end));
        }

        /// <summary>
        /// Bruker kun klokkeslettet fra innsendte ressurstider; døgnet bestemmes av <see cref="ResourceTimes.Place"/>.
        /// </summary>
        private static (DateTime Start, DateTime End) PlaceResource(ResourceRequest request, DateTime eventStart, DateTime eventEnd)
        {
            var start = ParseDateTime(request.StartTime, InvalidResourceStartTimeMessage);
            var end = ParseDateTime(request.EndTime, InvalidResourceEndTimeMessage);
            return ResourceTimes.Place(eventStart, eventEnd, start.TimeOfDay, end.TimeOfDay);
        }

        /// <summary>Ny ressurs. En eventuell <see cref="ResourceRequest.Id"/> ignoreres; id-en settes av databasen.</summary>
        private static EventResource Map(ResourceRequest request, DateTime eventStart, DateTime eventEnd) =>
            Map(request, PlaceResource(request, eventStart, eventEnd));

        private static EventResource Map(ResourceRequest request, (DateTime Start, DateTime End) times)
        {
            var (start, end) = times;
            return new EventResource
            {
                ResourceTypeId = request.ResourceTypeId,
                StartTime = start,
                EndTime = end,
                MinimumStaff = request.MinimumStaff,
            };
        }

        public async Task<IEnumerable<MessageResponse>> GetMessages(int eventResourceId)
        {
            return await Reader.GetMessages(eventResourceId);
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

            return await Reader.GetMessage(message.EventResourceMessageId)
                ?? throw new EntityNotFoundException();
        }

        public async Task<MessageResponse> DeleteMessage(int id, int eventResourceId)
        {
            var message = await DbContext.Messages
                .SingleOrDefaultAsync(m => m.EventResourceId == eventResourceId && m.EventResourceMessageId == id)
                ?? throw new EntityNotFoundException();

            if (!MessagePolicy.CanDelete(CurrentUser.ToActor(), message))
                throw new ForbiddenAccessException();

            // Svaret leses før slettingen.
            var response = await Reader.GetMessage(id)
                ?? throw new EntityNotFoundException();

            DbContext.Remove(message);
            await DbContext.SaveChangesAsync();

            return response;
        }
    }
}
