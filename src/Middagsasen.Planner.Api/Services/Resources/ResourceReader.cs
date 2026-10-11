using Microsoft.EntityFrameworkCore;
using Middagsasen.Planner.Api.Core;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services.Attachments;
using Middagsasen.Planner.Api.Services.Competencies;
using Middagsasen.Planner.Api.Services.Events;
using Middagsasen.Planner.Api.Services.ResourceTypes;
using Middagsasen.Planner.Api.Services.Shifts;

namespace Middagsasen.Planner.Api.Services.Resources
{
    /// <summary>
    /// Bevisst unntak fra repository-mønsteret: en ren lesemodell (spørringer og mapping, uten forretningsregler for
    /// skriving), så den går rett mot <see cref="PlannerDbContext"/>. Skrivesiden bruker fortsatt repository.
    /// </summary>
    public class ResourceReader : IResourceReader
    {
        public ResourceReader(PlannerDbContext dbContext, TimeProvider timeProvider)
        {
            DbContext = dbContext;
            TimeProvider = timeProvider;
        }

        public PlannerDbContext DbContext { get; }
        public TimeProvider TimeProvider { get; }

        // --- Includes: det eneste stedet som vet hva mappingen trenger ---

        private IQueryable<ResourceType> ResourceTypes => DbContext.ResourceTypes
            .Include(r => r.Trainers).ThenInclude(t => t.User)
            .Include(r => r.Files).ThenInclude(f => f.CreatedByUser)
            .Include(r => r.Files).ThenInclude(f => f.UpdatedByUser)
            .AsNoTracking()
            .AsSplitQuery();

        /// <summary>
        /// Navigasjonene mappingen av en oppgave trenger, relativt til <see cref="EventResource"/>. Samme liste brukes for
        /// oppgaver og (med prefikset <c>Resources</c>) for arrangementer, så de to ikke kan gli fra hverandre.
        /// </summary>
        private static readonly string[] ResourceIncludePaths =
        [
            Path(nameof(EventResource.Shifts), nameof(EventResourceUser.User), nameof(User.Trainings)),
            Path(nameof(EventResource.Shifts), nameof(EventResourceUser.User), nameof(User.Competencies)),
            Path(nameof(EventResource.ResourceType), nameof(ResourceType.Trainers), nameof(ResourceTypeTrainer.User)),
            Path(nameof(EventResource.ResourceType), nameof(ResourceType.Files), nameof(ResourceTypeFile.CreatedByUser)),
            Path(nameof(EventResource.ResourceType), nameof(ResourceType.Files), nameof(ResourceTypeFile.UpdatedByUser)),
            Path(nameof(EventResource.ResourceType), nameof(ResourceType.RequiredCompetencies), nameof(ResourceTypeCompetency.Competency)),
            Path(nameof(EventResource.Messages), nameof(EventResourceMessage.CreatedByUser)),
        ];

        private static string Path(params string[] navigations) => string.Join('.', navigations);

        private static IQueryable<T> WithResourceIncludes<T>(IQueryable<T> query, string? prefix) where T : class
            => ResourceIncludePaths
                .Aggregate(query, (q, path) => q.Include(prefix is null ? path : Path(prefix, path)))
                .AsNoTracking()
                .AsSplitQuery();

        private IQueryable<EventResource> Resources => WithResourceIncludes(DbContext.EventResource, null);

        private IQueryable<Event> Events => WithResourceIncludes(DbContext.Events, nameof(Event.Resources))
            .Include(e => e.CompetencyRequirements).ThenInclude(c => c.Competency);

        private IQueryable<EventResourceMessage> Messages => DbContext.Messages
            .Include(m => m.CreatedByUser)
            .AsNoTracking();

        // --- Arrangementer og oppgaver (med flagg) ---

        public async Task<IReadOnlyList<EventResponse>> GetEvents(Actor actor, DateTime? start = null, DateTime? end = null)
        {
            var query = Events;
            if (start is { } from)
                query = query.Where(e => e.StartTime >= from);
            if (end is { } to)
                query = query.Where(e => e.StartTime < to);

            var events = await query.ToListAsync();
            var viewer = await CreateViewer(actor, events.SelectMany(e => e.Resources));
            return events.Select(viewer.Map).ToList();
        }

        public async Task<EventResponse?> GetEvent(Actor actor, int eventId)
        {
            var evnt = await Events.SingleOrDefaultAsync(e => e.EventId == eventId);
            if (evnt is null)
                return null;

            var viewer = await CreateViewer(actor, evnt.Resources);
            return viewer.Map(evnt);
        }

        public async Task<ResourceResponse?> GetResource(Actor actor, int resourceId)
        {
            var resource = await Resources.SingleOrDefaultAsync(r => r.EventResourceId == resourceId);
            if (resource is null)
                return null;

            var viewer = await CreateViewer(actor, [resource]);
            return viewer.Map(resource);
        }

        /// <summary>
        /// Henter det flaggene for <paramref name="actor"/> trenger, med én spørring per tabell uansett antall oppgaver:
        /// opplæringsradene og kvitteringene. Kvitteringer hentes for innlogget bruker og, for admin, for eierne av
        /// vaktene i <paramref name="resources"/> (varselikonet); vanlige brukere ser bare sin egen status.
        /// </summary>
        private async Task<Viewer> CreateViewer(Actor actor, IEnumerable<EventResource> resources)
        {
            var trainingResourceTypeIds = await DbContext.ResourceTypeTrainings
                .Where(t => t.UserId == actor.UserId)
                .Select(t => t.ResourceTypeId)
                .ToListAsync();

            var resourceList = resources.ToList();
            var userIds = new HashSet<int> { actor.UserId };
            if (actor.IsAdmin)
                userIds.UnionWith(resourceList.SelectMany(r => r.Shifts).Select(s => s.UserId));
            var resourceTypeIds = resourceList.Select(r => r.ResourceTypeId).Distinct().ToList();

            // Kvitteringstidspunktene trengs bare for innlogget bruker (AcknowledgedAt); for flaggene holder settet av fil-id-er.
            var actorAcknowledgements = new Dictionary<int, DateTime>();
            var acknowledgedIdsByUser = new Dictionary<int, IReadOnlySet<int>>();
            if (resourceTypeIds.Count > 0)
            {
                var acknowledgements = await DbContext.ResourceTypeFileAcknowledgements
                    .Where(a => userIds.Contains(a.UserId) && resourceTypeIds.Contains(a.File.ResourceTypeId))
                    .Select(a => new { a.UserId, a.ResourceTypeFileId, a.Acknowledged })
                    .ToListAsync();
                foreach (var group in acknowledgements.GroupBy(a => a.UserId))
                    acknowledgedIdsByUser[group.Key] = group.Select(a => a.ResourceTypeFileId).ToHashSet();
                foreach (var a in acknowledgements.Where(a => a.UserId == actor.UserId))
                    actorAcknowledgements[a.ResourceTypeFileId] = a.Acknowledged;
            }

            return new Viewer(actor, TimeProvider.GetUtcNow(), trainingResourceTypeIds, actorAcknowledgements, acknowledgedIdsByUser);
        }

        // --- Vakttyper ---

        public async Task<IReadOnlyList<ResourceTypeResponse>> GetResourceTypes()
        {
            var resourceTypes = await ResourceTypes
                .Where(r => !r.Inactive)
                .ToListAsync();
            return resourceTypes.Select(r => ResourceMapping.MapResourceType(r)).ToList();
        }

        public async Task<IReadOnlyDictionary<int, ResourceTypeResponse>> GetResourceTypes(IEnumerable<int> ids)
        {
            var idList = ids.Distinct().ToList();
            if (idList.Count == 0)
                return new Dictionary<int, ResourceTypeResponse>();

            var resourceTypes = await ResourceTypes
                .Where(r => idList.Contains(r.ResourceTypeId))
                .ToListAsync();
            return resourceTypes.ToDictionary(r => r.ResourceTypeId, r => ResourceMapping.MapResourceType(r));
        }

        public async Task<ResourceTypeResponse?> GetResourceType(int id)
        {
            var resourceType = await ResourceTypes.SingleOrDefaultAsync(r => r.ResourceTypeId == id);
            return resourceType is null ? null : ResourceMapping.MapResourceType(resourceType);
        }

        // --- Opplæring, meldinger og filer ---

        public async Task<TrainingResponse?> GetTraining(int trainingId)
        {
            var training = await DbContext.ResourceTypeTrainings
                .Include(t => t.ResourceType)
                .Include(t => t.ConfirmedByUser)
                .AsNoTracking()
                .SingleOrDefaultAsync(t => t.ResourceTypeTrainingId == trainingId);
            return training is null ? null : ResourceMapping.MapTraining(training);
        }

        public async Task<IReadOnlyList<MessageResponse>> GetMessages(int resourceId)
        {
            var messages = await Messages
                .Where(m => m.EventResourceId == resourceId)
                .OrderBy(m => m.Created).ThenBy(m => m.EventResourceMessageId)
                .ToListAsync();
            return messages.Select(ResourceMapping.MapMessage).ToList();
        }

        public async Task<MessageResponse?> GetMessage(int messageId)
        {
            var message = await Messages.SingleOrDefaultAsync(m => m.EventResourceMessageId == messageId);
            return message is null ? null : ResourceMapping.MapMessage(message);
        }

        /// <summary>
        /// Mapper arrangementer og oppgaver med flagg for én innlogget bruker. Oppgavene må være lastet med
        /// <see cref="ResourceIncludePaths"/>.
        /// </summary>
        private sealed class Viewer
        {
            /// <param name="actor">Innlogget bruker.</param>
            /// <param name="utcNow">Nå.</param>
            /// <param name="trainingResourceTypeIds">Vakttypene innlogget bruker har en opplæringsrad for.</param>
            /// <param name="actorAcknowledgements">Innlogget brukers kvitteringer (vedlegg-id → tidspunkt), til <c>AcknowledgedAt</c>.</param>
            /// <param name="acknowledgedIdsByUser">Kvitterte vedlegg-id-er per bruker, for innlogget bruker og (admin) vaktenes eiere.</param>
            public Viewer(Actor actor, DateTimeOffset utcNow, IEnumerable<int> trainingResourceTypeIds,
                IReadOnlyDictionary<int, DateTime> actorAcknowledgements, IReadOnlyDictionary<int, IReadOnlySet<int>> acknowledgedIdsByUser)
            {
                Actor = actor;
                Now = utcNow.ToNorwegianLocalTime();
                UtcNow = utcNow.UtcDateTime;
                TrainingResourceTypeIds = trainingResourceTypeIds.ToHashSet();
                ActorAcknowledgements = actorAcknowledgements;
                AcknowledgedIdsByUser = acknowledgedIdsByUser;
            }

            private static readonly IReadOnlySet<int> NoAcknowledgements = new HashSet<int>();

            private Actor Actor { get; }

            /// <summary>Nå i norsk lokal tid, som oppgavenes tider lagres i.</summary>
            private DateTime Now { get; }

            /// <summary>Nå i UTC, som kompetansenes utløpsdato sammenlignes med for vaktkravene.</summary>
            private DateTime UtcNow { get; }

            private HashSet<int> TrainingResourceTypeIds { get; }

            private IReadOnlyDictionary<int, DateTime> ActorAcknowledgements { get; }

            private IReadOnlyDictionary<int, IReadOnlySet<int>> AcknowledgedIdsByUser { get; }

            /// <summary>Brukeren mangler kvittering på en påkrevd instruks på vakttypen (<see cref="AttachmentRules.HasBlockingInstruction"/>).</summary>
            private bool HasUnacknowledgedMandatoryInstructions(ResourceType resourceType, int userId)
                => AttachmentRules.HasBlockingInstruction(resourceType.Files, AcknowledgedIdsByUser.GetValueOrDefault(userId) ?? NoAcknowledgements);

            public EventResponse Map(Event evnt) => new()
            {
                Id = evnt.EventId,
                Name = evnt.Name,
                Description = evnt.Description,
                StartTime = evnt.StartTime.ToSimpleIsoString(),
                EndTime = evnt.EndTime.ToSimpleIsoString(),
                Resources = evnt.Resources.Select(Map).OrderBy(r => r.ResourceType.Id).ThenBy(r => r.StartTime).ToList(),
                CompetencyRequirements = CompetencyRequirementSet.MapActive(evnt.CompetencyRequirements),
                CompetencyWarnings = GetFacilityCompetencyWarnings(evnt),
            };

            /// <summary>
            /// Brudd på anleggskravene i åpningstiden. Krav til inaktive (slettede) kompetanser hoppes over. Alle bemannede
            /// vakter teller, uansett vakttype, når brukeren har en gyldig kompetanse (<see cref="CompetencyRules.IsValid"/>)
            /// <b>da vaktlisten starter</b>, ikke nå som for vaktkravene: en kompetanse som utløper før
            /// vaktlisten starter, teller ikke, og en vaktliste i fortiden får ikke nye advarsler fordi en kompetanse har
            /// utløpt senere. Godkjenning vurderes som den er nå.
            /// </summary>
            private static List<FacilityCompetencyWarningResponse> GetFacilityCompetencyWarnings(Event evnt)
            {
                var warnings = new List<FacilityCompetencyWarningResponse>();

                // Vaktlistens tider er norsk lokal tid; utløpsdatoene er UTC.
                var validAtUtc = evnt.StartTime.NorwegianLocalTimeToUtc();

                foreach (var requirement in CompetencyRequirementSet.Active(evnt.CompetencyRequirements))
                {
                    var periods = evnt.Resources
                        .SelectMany(resource => resource.Shifts
                            .Where(s => s.User?.Competencies.Any(uc =>
                                uc.CompetencyId == requirement.CompetencyId && CompetencyRules.IsValid(uc, validAtUtc)) == true)
                            .Select(s =>
                            {
                                var (start, end) = FacilityRequirementRules.EffectivePeriod(
                                    s.StartTime, s.EndTime, resource.StartTime, resource.EndTime);
                                return new FacilityRequirementRules.StaffedPeriod(s.UserId, start, end);
                            }));

                    var breaches = FacilityRequirementRules.FindBreaches(
                        evnt.StartTime, evnt.EndTime, requirement.MinimumRequired, periods);

                    warnings.AddRange(breaches.Select(b => new FacilityCompetencyWarningResponse
                    {
                        CompetencyId = requirement.CompetencyId,
                        CompetencyName = requirement.Competency.Name,
                        MinimumRequired = requirement.MinimumRequired,
                        CurrentCount = b.Count,
                        StartTime = b.Start.ToSimpleIsoString(),
                        EndTime = b.End.ToSimpleIsoString(),
                    }));
                }

                // Starttiden har fast format (yyyy-MM-ddTHH:mm), så ordinal sortering er kronologisk.
                return warnings
                    .OrderBy(w => w.CompetencyName)
                    .ThenBy(w => w.CompetencyId)
                    .ThenBy(w => w.StartTime, StringComparer.Ordinal)
                    .ToList();
            }

            public ResourceResponse Map(EventResource resource)
            {
                var facts = ShiftFactsFactory.From(resource);
                return new ResourceResponse
                {
                    Id = resource.EventResourceId,
                    EventId = resource.EventId,
                    ResourceType = ResourceMapping.MapResourceType(resource.ResourceType, ActorAcknowledgements),
                    StartTime = resource.StartTime.ToSimpleIsoString(),
                    EndTime = resource.EndTime.ToSimpleIsoString(),
                    ShiftCount = resource.ShiftCount,
                    Shifts = resource.Shifts
                        .OrderBy(s => s.EventResourceUserId)
                        .Select(s => MapShift(s, resource.ResourceType, facts, facts.Shifts.Single(f => f.ShiftId == s.EventResourceUserId)))
                        .ToList(),
                    Messages = resource.Messages
                        .OrderBy(m => m.Created).ThenBy(m => m.EventResourceMessageId)
                        .Select(ResourceMapping.MapMessage)
                        .ToList(),
                    CompetencyWarnings = GetCompetencyWarnings(resource),
                    IsMissingStaff = ShiftRules.IsMissingStaff(facts),
                    IsFull = ShiftRules.IsFull(facts),
                    IsPast = ShiftRules.IsPast(facts, Now),
                    CanSignUp = ShiftRules.CanSignUp(Actor, facts, Now),
                    MustAnswerTraining = ShiftRules.MustAnswerTraining(facts, TrainingResourceTypeIds.Contains(resource.ResourceTypeId)),
                    HasUnacknowledgedMandatoryInstructions = HasUnacknowledgedMandatoryInstructions(resource.ResourceType, Actor.UserId),
                };
            }

            private ShiftResponse MapShift(EventResourceUser shift, ResourceType resourceType, ResourceFacts resource, ShiftFacts facts) => new()
            {
                // Andres kvitteringsstatus er bare synlig for admin; eieren ser sin egen.
                HasUnacknowledgedMandatoryInstructions = Actor.IsAdminOrSelf(shift.UserId)
                    && HasUnacknowledgedMandatoryInstructions(resourceType, shift.UserId),
                Id = shift.EventResourceUserId,
                EventResourceId = shift.EventResourceId,
                User = ResourceMapping.MapShiftUser(shift.User),
                StartTime = shift.StartTime.ToSimpleIsoString(),
                EndTime = shift.EndTime.ToSimpleIsoString(),
                Comment = shift.Comment,
                NeedsTraining = facts.NeedsTraining,
                IsMine = shift.UserId == Actor.UserId,
                CanEdit = ShiftRules.CanEdit(Actor, resource, Now, facts),
                CanWithdraw = ShiftRules.CanWithdraw(Actor, resource, Now, facts),
                CanConfirmTraining = ShiftRules.CanConfirmTraining(Actor, resource, Now, facts),
            };

            /// <summary>
            /// Vaktkrav for vakttypen som ikke er oppfylt av vaktene. En vakt teller når brukeren har en
            /// gyldig kompetanse (<see cref="CompetencyRules.IsValid"/>).
            /// </summary>
            private List<CompetencyWarningResponse> GetCompetencyWarnings(EventResource resource)
            {
                var warnings = new List<CompetencyWarningResponse>();

                foreach (var rc in resource.ResourceType.RequiredCompetencies)
                {
                    if (rc.Competency == null) continue;

                    var count = resource.Shifts.Count(s => s.User?.Competencies.Any(uc =>
                        uc.CompetencyId == rc.CompetencyId && CompetencyRules.IsValid(uc, UtcNow)) == true);

                    if (count < rc.MinimumRequired)
                    {
                        warnings.Add(new CompetencyWarningResponse
                        {
                            CompetencyName = rc.Competency.Name,
                            MinimumRequired = rc.MinimumRequired,
                            CurrentCount = count,
                        });
                    }
                }

                return warnings;
            }
        }
    }
}
