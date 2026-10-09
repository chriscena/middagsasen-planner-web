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
            await CompetencyRequirementSet.Validate(DbContext, request.CompetencyRequirements);
            var newEvent = new Event
            {
                Name = request.Name,
                Description = request.Description,
                StartTime = eventStart,
                EndTime = eventEnd,
                // Slettede ressurser (IsDeleted) finnes ikke fra før og skal ikke opprettes.
                Resources = request.Resources.Where(r => !r.IsDeleted).Select(r => Map(r, eventStart, eventEnd)).ToList(),
            };
            CompetencyRequirementSet.Apply(newEvent.CompetencyRequirements, request.CompetencyRequirements);

            DbContext.Events.Add(newEvent);
            await DbContext.SaveChangesAsync();

            return await GetEventById(newEvent.EventId);
        }

        /// <summary>
        /// Lagrer vaktlisteskjemaet. Alt lagres i én transaksjon: ved konflikt på antall vakter lagres ingenting,
        /// heller ikke navn og tider.
        /// <para>
        /// Antall vakter (#151) på eksisterende ressurser med <see cref="ResourceRequest.OriginalMinimumStaff"/> avgjøres mot
        /// verdien som er lagret nå («sammenlign og sett», se <see cref="ApplyMinimumStaff"/>), slik at ledige plasser andre har
        /// lagt til eller fjernet i mellomtiden, ikke overskrives i stillhet. Uten <see cref="ResourceRequest.OriginalMinimumStaff"/>
        /// settes verdien absolutt, som før.
        /// </para>
        /// <para>
        /// Låsing (<see cref="RowLocks"/>): vaktlista låses først, så alle ressursene i den. Det serialiserer samtidige lagringer
        /// av samme vaktliste, og det er samme ressurslås som påmelding og «Legg til/Fjern ledig plass» bruker
        /// (<see cref="IShiftRepository.InResourceLock{T}"/>). Dermed skjer også endringer i tider og vakttype under
        /// ressurslåsen, så en samtidig påmelding vurderes enten mot de gamle eller de nye tidene, ikke en blanding.
        /// Vaktlista og ressursene lastes først etter låsen, slik at de sporede entitetene har fersk <c>MinimumStaff</c>.
        /// </para>
        /// <para>
        /// Anleggskravene erstattes av <see cref="EventRequest.CompetencyRequirements"/> i samme transaksjon, og beholdes
        /// uendret når feltet er <c>null</c>.
        /// </para>
        /// <para>
        /// Bemannede vakter følger ressursens tider (#173): endres tidene på en eksisterende ressurs (f.eks. når vaktlista
        /// flyttes til en annen dato, eller oppgaven utvides), justeres vaktene med <see cref="ResourceTimes.FollowResource"/>
        /// i samme transaksjon og under samme ressurslås.
        /// </para>
        /// </summary>
        /// <exception cref="DomainValidationException">Anleggskravene er ugyldige.</exception>
        /// <exception cref="ConcurrentUpdateException">Antall vakter er endret av noen andre siden skjemaet ble lastet.</exception>
        public async Task<EventResponse> UpdateEvent(int eventId, EventRequest request)
        {
            var (eventStart, eventEnd) = EventTimes(request);

            await using (var transaction = await DbContext.Database.BeginTransactionAsync())
            {
                if (!await DbContext.LockEvent(eventId))
                    throw new EntityNotFoundException(EventNotFoundMessage);
                await DbContext.LockEventResources(eventId);

                var existingEvent = await DbContext.Events
                    .Include(e => e.Resources)
                    .Include(e => e.CompetencyRequirements)
                    .SingleOrDefaultAsync(e => e.EventId == eventId)
                    ?? throw new EntityNotFoundException(EventNotFoundMessage);

                await CompetencyRequirementSet.Validate(DbContext, request.CompetencyRequirements);
                CompetencyRequirementSet.Apply(existingEvent.CompetencyRequirements, request.CompetencyRequirements);

                existingEvent.Name = request.Name;
                existingEvent.Description = request.Description;
                // Døgnforskyvningen vaktene følger, fra vaktlistas flytting (ikke ressursens).
                var dayShift = ResourceTimes.DayShift(existingEvent.StartTime, eventStart);
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
                        // Før vakttype og tider endres, så en konfliktmelding viser de lagrede verdiene.
                        await ApplyMinimumStaff(resource, resourceToUpdate);
                        resourceToUpdate.ResourceTypeId = resource.ResourceTypeId;
                        var (oldStart, oldEnd) = (resourceToUpdate.StartTime, resourceToUpdate.EndTime);
                        (resourceToUpdate.StartTime, resourceToUpdate.EndTime) = PlaceResource(resource, eventStart, eventEnd);
                        await FollowResource(resourceToUpdate, oldStart, oldEnd, dayShift);
                    }
                }

                await DbContext.SaveChangesAsync();
                await transaction.CommitAsync();
            }

            return await GetEventById(eventId);
        }

        /// <summary>
        /// Setter antall vakter på en eksisterende ressurs. <paramref name="stored"/> er lest under ressurslåsen, så verdien er fersk.
        /// Med <see cref="ResourceRequest.OriginalMinimumStaff"/> («sammenlign og sett»):
        /// <list type="bullet">
        /// <item>Uendret i skjemaet (lik original): ingenting skrives, så andres endringer beholdes.</item>
        /// <item>Lagret verdi er allerede lik skjemaets (f.eks. ved ny lagring av samme skjema): ingenting skrives, ingen feil.</item>
        /// <item>Lagret verdi er lik original (ingen andre har endret den): skjemaets verdi settes som absolutt verdi. Det er lov
        /// å gå under antall bemannede vakter, som ellers i skjemaet.</item>
        /// <item>Ellers har noen andre endret verdien: <see cref="ConcurrentUpdateException"/>.</item>
        /// </list>
        /// Uten original settes skjemaets verdi absolutt.
        /// </summary>
        private async Task ApplyMinimumStaff(ResourceRequest request, EventResource stored)
        {
            if (request.OriginalMinimumStaff is not { } original)
            {
                stored.MinimumStaff = request.MinimumStaff;
                return;
            }

            if (request.MinimumStaff == original || stored.MinimumStaff == request.MinimumStaff)
                return;

            if (stored.MinimumStaff != original)
            {
                var resourceTypeName = await DbContext.ResourceTypes
                    .Where(t => t.ResourceTypeId == stored.ResourceTypeId)
                    .Select(t => t.Name)
                    .SingleAsync();
                throw new ConcurrentUpdateException(
                    StaffingChangedMessage(resourceTypeName, stored.StartTime, stored.EndTime, stored.MinimumStaff));
            }

            stored.MinimumStaff = request.MinimumStaff;
        }

        /// <summary>
        /// Justerer vaktene på <paramref name="resource"/> etter at tidene er endret fra (<paramref name="oldStart"/>,
        /// <paramref name="oldEnd"/>), med vaktlistas døgnforskyvning <paramref name="dayShift"/>. Uendrede tider rører ingen
        /// vakter, og da lastes de heller ikke. Ellers lastes vaktene for akkurat denne ressursen, under ressurslåsen.
        /// </summary>
        private async Task FollowResource(EventResource resource, DateTime oldStart, DateTime oldEnd, TimeSpan dayShift)
        {
            if (resource.StartTime == oldStart && resource.EndTime == oldEnd) return;

            await DbContext.Entry(resource).Collection(r => r.Shifts).LoadAsync();
            foreach (var shift in resource.Shifts)
            {
                (shift.StartTime, shift.EndTime) = ResourceTimes.FollowResource(
                    oldStart, oldEnd, resource.StartTime, resource.EndTime, dayShift, shift.StartTime, shift.EndTime);
            }
        }

        /// <summary>Konfliktmelding med de lagrede verdiene til ressursen.</summary>
        internal static string StaffingChangedMessage(string resourceTypeName, DateTime start, DateTime end, int currentMinimumStaff)
            => $"Antall vakter på {resourceTypeName} {start:HH\\:mm}–{end:HH\\:mm} er endret av noen andre (nå {currentMinimumStaff}). "
                + "Last vaktlista på nytt og prøv igjen.";

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
                .Include(e => e.CompetencyRequirements).ThenInclude(r => r.Competency)
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
                CompetencyRequirements = CompetencyRequirementSet.CopyToEvent(template.CompetencyRequirements),
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
