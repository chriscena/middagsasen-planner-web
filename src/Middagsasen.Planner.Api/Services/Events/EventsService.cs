using Microsoft.EntityFrameworkCore;
using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Core;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services.Resources;
using Middagsasen.Planner.Api.Services.Shifts;

namespace Middagsasen.Planner.Api.Services.Events
{
    public class EventsService : IEventsService
    {
        internal const string MessageEmptyMessage = "Beskjeden kan ikke være tom.";
        internal static readonly string MessageTooLongMessage = $"Beskjeden kan ikke være lengre enn {MessageRequest.MaxLength} tegn.";

        internal const string EventNotFoundMessage = "Fant ikke arrangementet.";

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
            var (eventStart, eventEnd) = EventTimes(request);
            var newEvent = new Event
            {
                Name = request.Name,
                Description = request.Description,
                StartTime = eventStart,
                EndTime = eventEnd,
                // Slettede ressurser (IsDeleted) finnes ikke fra før og skal ikke opprettes.
                Resources = request.Resources.Where(r => !r.IsDeleted).Select(r => Map(r, eventStart, eventEnd)).ToList(),
            };

            DbContext.Events.Add(newEvent);
            await DbContext.SaveChangesAsync();

            return await GetEventById(newEvent.EventId);
        }

        /// <summary>
        /// Lagrer vaktlisteskjemaet. Alt lagres i én transaksjon: endres bemanningen på en ressurs til noe reglene ikke tillater,
        /// lagres ingenting, heller ikke navn og tider.
        /// <para>
        /// Bemanning (#151): for eksisterende ressurser med <see cref="ResourceRequest.OriginalMinimumStaff"/> legges endringen
        /// admin gjorde i skjemaet, på verdien som er lagret nå (<see cref="ShiftRules.MinimumStaffAfterChange"/>), slik at
        /// ledige plasser andre har lagt til eller fjernet i mellomtiden, beholdes. Uendret bemanning skrives ikke. Uten
        /// <see cref="ResourceRequest.OriginalMinimumStaff"/> settes verdien absolutt, som før.
        /// </para>
        /// <para>
        /// Låsing: vaktlista låses først (serialiserer samtidige lagringer av samme vaktliste), deretter ressursene der
        /// bemanningen kan endres, i stigende id-rekkefølge (<see cref="RowLocks"/>). Det er samme ressurslås som påmelding og
        /// «Legg til/Fjern ledig plass» bruker (<see cref="IShiftRepository.InResourceLock{T}"/>), så bemanningen
        /// leses og skrives mot ferske data. Vaktlista og ressursene leses først etter låsen, slik at de sporede entitetene
        /// har fersk <c>MinimumStaff</c> og ikke skriver en gammel verdi tilbake.
        /// </para>
        /// </summary>
        public async Task<EventResponse> UpdateEvent(int eventId, EventRequest request)
        {
            var (eventStart, eventEnd) = EventTimes(request);

            await using (var transaction = await DbContext.Database.BeginTransactionAsync())
            {
                if (!await DbContext.LockEvent(eventId))
                    throw new EntityNotFoundException(EventNotFoundMessage);

                // Eksisterende ressurser der bemanningen kan endres: relativ endring som ikke er 0, eller absolutt verdi
                // (OriginalMinimumStaff == null; om den faktisk endres, vet vi først når den ferske verdien er lest).
                var staffingCandidates = request.Resources
                    .Where(r => !r.IsDeleted && r.Id.HasValue && r.MinimumStaff != r.OriginalMinimumStaff)
                    .Select(r => r.Id!.Value);
                var lockedResourceIds = (await DbContext.LockResources(eventId, staffingCandidates)).ToHashSet();

                var existingEvent = await DbContext.Events
                    .Include(e => e.Resources)
                    .SingleOrDefaultAsync(e => e.EventId == eventId)
                    ?? throw new EntityNotFoundException(EventNotFoundMessage);

                var shiftCounts = lockedResourceIds.Count == 0
                    ? []
                    : await DbContext.Shifts
                        .Where(s => lockedResourceIds.Contains(s.EventResourceId))
                        .GroupBy(s => s.EventResourceId)
                        .Select(g => new { ResourceId = g.Key, Count = g.Count() })
                        .ToDictionaryAsync(g => g.ResourceId, g => g.Count);

                existingEvent.Name = request.Name;
                existingEvent.Description = request.Description;
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
                        // Bemanningen endres bare på låste ressurser. På de andre er den uendret, og EF skriver den ikke.
                        if (lockedResourceIds.Contains(resourceToUpdate.EventResourceId))
                        {
                            var staffing = new ResourceStaffing(resourceToUpdate.MinimumStaff, shiftCounts.GetValueOrDefault(resourceToUpdate.EventResourceId));
                            resourceToUpdate.MinimumStaff = await NewMinimumStaff(resource, staffing);
                        }
                    }
                }

                await DbContext.SaveChangesAsync();
                await transaction.CommitAsync();
            }

            return await GetEventById(eventId);
        }

        /// <summary>
        /// Ny <c>MinimumStaff</c> for en eksisterende ressurs, regnet ut fra <paramref name="staffing"/> lest under ressurslåsen.
        /// </summary>
        /// <exception cref="DomainValidationException">Endringen ville fjernet flere plasser enn det er ledige.</exception>
        private async Task<int> NewMinimumStaff(ResourceRequest request, ResourceStaffing staffing)
        {
            if (request.OriginalMinimumStaff is not { } original)
                return request.MinimumStaff;

            var change = request.MinimumStaff - original;
            if (ShiftRules.MinimumStaffAfterChange(staffing, change) is { } minimumStaff)
                return minimumStaff;

            var resourceTypeName = await DbContext.ResourceTypes
                .Where(t => t.ResourceTypeId == request.ResourceTypeId)
                .Select(t => t.Name)
                .SingleOrDefaultAsync() ?? "vakttypen";
            throw new DomainValidationException(TooFewEmptySlotsMessage(
                resourceTypeName, request.StartTime, request.EndTime, -change, ShiftRules.EmptySlots(staffing)));
        }

        internal static string TooFewEmptySlotsMessage(string resourceTypeName, TimeOnly start, TimeOnly end, int removed, int emptySlots)
        {
            var removedText = removed == 1 ? "1 vakt" : $"{removed} vakter";
            var emptyText = emptySlots switch
            {
                0 => "ingen av vaktene er ledige",
                1 => "bare 1 av vaktene er ledig",
                _ => $"bare {emptySlots} av vaktene er ledige",
            };
            return $"Kan ikke fjerne {removedText} på {resourceTypeName} {start:HH\\:mm}–{end:HH\\:mm}: {emptyText} nå. "
                + "Last vaktlista på nytt for å se gjeldende bemanning.";
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
            var startDay = request.StartDate.ToDateTime(TimeOnly.MinValue);

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

        private static (DateTime Start, DateTime End) EventTimes(EventRequest request) =>
            (request.StartTime, ResourceTimes.NormalizeEventEnd(request.StartTime, request.EndTime));

        /// <summary>
        /// Bruker kun klokkeslettet fra innsendte ressurstider; døgnet bestemmes av <see cref="ResourceTimes.Place"/>.
        /// </summary>
        private static (DateTime Start, DateTime End) PlaceResource(ResourceRequest request, DateTime eventStart, DateTime eventEnd) =>
            ResourceTimes.Place(eventStart, eventEnd, request.StartTime.ToTimeSpan(), request.EndTime.ToTimeSpan());

        /// <summary>Ny ressurs. En eventuell <see cref="ResourceRequest.Id"/> ignoreres; id-en settes av databasen.</summary>
        private static EventResource Map(ResourceRequest request, DateTime eventStart, DateTime eventEnd)
        {
            var (start, end) = PlaceResource(request, eventStart, eventEnd);
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
