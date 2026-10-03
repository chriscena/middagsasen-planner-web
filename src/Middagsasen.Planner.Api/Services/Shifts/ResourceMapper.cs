using Middagsasen.Planner.Api.Core;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services.Competencies;
using Middagsasen.Planner.Api.Services.Events;
using Middagsasen.Planner.Api.Services.ResourceTypes;

namespace Middagsasen.Planner.Api.Services.Shifts
{
    /// <summary>
    /// Mapper ressurser med vakter til <see cref="ResourceResponse"/> med flagg for innlogget bruker. Brukes både av
    /// lesesiden (<c>GET api/events</c>) og av skriveoperasjonene i <see cref="ShiftService"/>, slik at flaggene
    /// er like begge steder. Lages av <see cref="IShiftService.CreateResourceMapper"/>.
    /// <para>
    /// Ressursen må være lastet med: <c>ResourceType.Trainers.User</c>, <c>ResourceType.Files</c>,
    /// <c>ResourceType.RequiredCompetencies.Competency</c>, <c>Shifts.User.Trainings</c>,
    /// <c>Shifts.User.Competencies</c> og <c>Messages.CreatedByUser</c>
    /// (se <see cref="ShiftRepository.WithMappingIncludes(IQueryable{EventResource})"/>).
    /// </para>
    /// </summary>
    public sealed class ResourceMapper
    {
        /// <param name="actor">Innlogget bruker.</param>
        /// <param name="utcNow">Nå.</param>
        /// <param name="viewerTrainingResourceTypeIds">Ressurstypene innlogget bruker har en opplæringsrad for.</param>
        public ResourceMapper(Actor actor, DateTimeOffset utcNow, IEnumerable<int> viewerTrainingResourceTypeIds)
        {
            Actor = actor;
            Now = utcNow.ToNorwegianLocalTime();
            UtcNow = utcNow.UtcDateTime;
            ViewerTrainingResourceTypeIds = viewerTrainingResourceTypeIds.ToHashSet();
        }

        public Actor Actor { get; }

        /// <summary>Nå i norsk lokal tid, som ressursenes tider lagres i.</summary>
        public DateTime Now { get; }

        /// <summary>Nå i UTC, som kompetansenes utløpsdato sammenlignes med.</summary>
        public DateTime UtcNow { get; }

        public IReadOnlySet<int> ViewerTrainingResourceTypeIds { get; }

        /// <summary>
        /// Faktaene <see cref="ShiftRules"/> trenger om en ressurs. Krever <c>ResourceType.Trainers</c> og
        /// <c>Shifts.User.Trainings</c>.
        /// </summary>
        public static ResourceFacts ToFacts(EventResource resource) => new(
            resource.EventResourceId,
            resource.ResourceTypeId,
            resource.StartTime,
            resource.EndTime,
            resource.MinimumStaff,
            HasTraining: resource.ResourceType.Trainers.Count > 0,
            TrainerUserIds: resource.ResourceType.Trainers.Select(t => t.UserId).ToHashSet(),
            Shifts: resource.Shifts.Select(s => ToFacts(s, resource.ResourceTypeId)).ToList());

        private static ShiftFacts ToFacts(EventResourceUser shift, int resourceTypeId) => new(
            shift.EventResourceUserId,
            shift.UserId,
            NeedsTraining: shift.User?.Trainings.Any(t => t.ResourceTypeId == resourceTypeId && t.TrainingComplete == false) ?? false);

        public EventResponse Map(Event evnt) => new()
        {
            Id = evnt.EventId,
            Name = evnt.Name,
            Description = evnt.Description,
            StartTime = evnt.StartTime.ToSimpleIsoString(),
            EndTime = evnt.EndTime.ToSimpleIsoString(),
            Resources = evnt.Resources.Select(Map).OrderBy(r => r.ResourceType.Id).ThenBy(r => r.StartTime).ToList(),
        };

        public ResourceResponse Map(EventResource resource)
        {
            var facts = ToFacts(resource);
            return new ResourceResponse
            {
                Id = resource.EventResourceId,
                EventId = resource.EventId,
                ResourceType = MapResourceType(resource.ResourceType),
                StartTime = resource.StartTime.ToSimpleIsoString(),
                EndTime = resource.EndTime.ToSimpleIsoString(),
                MinimumStaff = resource.MinimumStaff,
                Shifts = resource.Shifts
                    .OrderBy(s => s.EventResourceUserId)
                    .Select(s => MapShift(s, facts, facts.Shifts.Single(f => f.ShiftId == s.EventResourceUserId)))
                    .ToList(),
                Messages = resource.Messages.Select(MapMessage).ToList(),
                CompetencyWarnings = GetCompetencyWarnings(resource),
                IsMissingStaff = ShiftRules.IsMissingStaff(facts),
                IsFull = ShiftRules.IsFull(facts),
                IsPast = ShiftRules.IsPast(facts, Now),
                CanSignUp = ShiftRules.CanSignUp(Actor, facts, Now),
                MustAnswerTraining = ShiftRules.MustAnswerTraining(facts, ViewerTrainingResourceTypeIds.Contains(resource.ResourceTypeId)),
            };
        }

        private ShiftResponse MapShift(EventResourceUser shift, ResourceFacts resource, ShiftFacts facts) => new()
        {
            Id = shift.EventResourceUserId,
            EventResourceId = shift.EventResourceId,
            User = MapShiftUser(shift.User),
            StartTime = shift.StartTime,
            EndTime = shift.EndTime,
            Comment = shift.Comment,
            NeedsTraining = facts.NeedsTraining,
            IsMine = shift.UserId == Actor.UserId,
            CanEdit = ShiftRules.CanEdit(Actor, resource, Now, facts),
            CanWithdraw = ShiftRules.CanWithdraw(Actor, resource, Now, facts),
            CanConfirmTraining = ShiftRules.CanConfirmTraining(Actor, resource, Now, facts),
        };

        /// <summary>
        /// Kompetansekrav for ressurstypen som ikke er oppfylt av vaktene. En vakt teller når brukeren har en
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

        // --- Felles mappere uten flagg (brukes også av andre services) ---

        internal static TrainingResponse MapTraining(ResourceTypeTraining training) => new()
        {
            Id = training.ResourceTypeTrainingId,
            UserId = training.UserId,
            ResourceTypeId = training.ResourceTypeId,
            ResourceTypeName = training.ResourceType?.Name,
            TrainingComplete = training.TrainingComplete,
            Confirmed = training.Confirmed?.ToSimpleIsoString(),
            ConfirmedById = training.ConfirmedBy,
            ConfirmedByName = MapFullName(training.ConfirmedByUser?.FirstName, training.ConfirmedByUser?.LastName),
        };

        internal static ResourceTypeResponse MapResourceType(ResourceType resourceType) => new()
        {
            Id = resourceType.ResourceTypeId,
            Name = resourceType.Name,
            DefaultStaff = resourceType.DefaultStaff,
            NotificationMessage = resourceType.NotificationMessage,
            HasTraining = resourceType.Trainers.Any(),
            Trainers = resourceType.Trainers.Select(MapTrainer).ToList(),
            Files = resourceType.Files.Select(MapFile).ToList(),
        };

        private static ResourceTypeTrainerResponse MapTrainer(ResourceTypeTrainer trainer) => new()
        {
            Id = trainer.ResourceTypeTrainerId,
            UserId = trainer.UserId,
            FullName = MapFullName(trainer.User.FirstName, trainer.User.LastName),
            PhoneNo = trainer.User.UserName,
        };

        private static FileInfoResponse MapFile(ResourceTypeFile file) => new()
        {
            Id = file.ResourceTypeFileId,
            ResourceTypeId = file.ResourceTypeId,
            FileName = file.FileName,
            Description = file.Description,
            MimeType = file.MimeType,
            Created = file.Created.AsUtc().ToIsoString(),
            CreatedBy = MapFullName(file.CreatedByUser?.FirstName, file.CreatedByUser?.LastName),
            Updated = file.Updated.AsUtc().ToIsoString(),
            UpdatedBy = MapFullName(file.UpdatedByUser?.FirstName, file.UpdatedByUser?.LastName),
        };

        internal static MessageResponse MapMessage(EventResourceMessage message) => new()
        {
            Id = message.EventResourceMessageId,
            EventResourceId = message.EventResourceId,
            CreatedBy = MapShiftUser(message.CreatedByUser),
            Created = message.Created.AsUtc().ToIsoString(),
            Message = message.Message,
        };

        internal static ShiftUserResponse MapShiftUser(User user) => new()
        {
            Id = user.UserId,
            PhoneNumber = user.UserName,
            FirstName = user.FirstName,
            LastName = user.LastName,
            FullName = MapFullName(user.FirstName, user.LastName),
            Trainings = user.Trainings.Select(MapTraining).ToList(),
        };

        /// <summary>Fullt navn: fornavn og etternavn med mellomrom, uten mellomrom i endene når et av dem mangler.</summary>
        internal static string MapFullName(string? firstName, string? lastName)
            => $"{firstName ?? ""} {lastName ?? ""}".Trim();
    }
}
