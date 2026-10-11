using Middagsasen.Planner.Api.Core;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services.Attachments;
using Middagsasen.Planner.Api.Services.Events;
using Middagsasen.Planner.Api.Services.ResourceTypes;

namespace Middagsasen.Planner.Api.Services.Resources
{
    /// <summary>
    /// Mapping uten flagg for innlogget bruker. Bare <see cref="ResourceReader"/> bruker den, og den kjenner til hvilke
    /// navigasjoner som må være lastet. Vedlegg mappes av <see cref="AttachmentMapping"/>, som vedleggsmodulen eier.
    /// </summary>
    internal static class ResourceMapping
    {
        /// <summary>
        /// Krever <c>Trainers.User</c>, <c>Files.CreatedByUser</c> og <c>Files.UpdatedByUser</c>. Arkiverte vedlegg utelates.
        /// </summary>
        /// <param name="acknowledgedAt">
        /// Når innlogget bruker kvitterte, per vedlegg-id, eller <c>null</c> der vakttypen leses uten kjent bruker.
        /// </param>
        public static ResourceTypeResponse MapResourceType(ResourceType resourceType, IReadOnlyDictionary<int, DateTime>? acknowledgedAt = null) => new()
        {
            Id = resourceType.ResourceTypeId,
            Name = resourceType.Name,
            DefaultShiftCount = resourceType.DefaultShiftCount,
            NotificationMessage = resourceType.NotificationMessage,
            HasTraining = resourceType.Trainers.Count > 0,
            Trainers = resourceType.Trainers.OrderBy(t => t.ResourceTypeTrainerId).Select(MapTrainer).ToList(),
            Files = resourceType.Files
                .Where(f => f.Archived == null)
                .OrderBy(f => f.ResourceTypeFileId)
                .Select(f => AttachmentMapping.MapFile(f, acknowledgedAt is not null && acknowledgedAt.TryGetValue(f.ResourceTypeFileId, out var at) ? at : null))
                .ToList(),
        };

        private static ResourceTypeTrainerResponse MapTrainer(ResourceTypeTrainer trainer) => new()
        {
            Id = trainer.ResourceTypeTrainerId,
            UserId = trainer.UserId,
            FullName = trainer.User.FullName(),
            PhoneNo = trainer.User.UserName,
        };

        /// <summary>Krever <c>CreatedByUser</c>.</summary>
        public static MessageResponse MapMessage(EventResourceMessage message) => new()
        {
            Id = message.EventResourceMessageId,
            EventResourceId = message.EventResourceId,
            CreatedBy = MapShiftUser(message.CreatedByUser),
            Created = message.Created.AsUtc().ToIsoString(),
            Message = message.Message,
        };

        /// <summary>
        /// Opplæringen. <c>ResourceType</c> og <c>ConfirmedByUser</c> brukes hvis de er lastet (ellers blir navnene tomme).
        /// </summary>
        public static TrainingResponse MapTraining(ResourceTypeTraining training) => new()
        {
            Id = training.ResourceTypeTrainingId,
            UserId = training.UserId,
            ResourceTypeId = training.ResourceTypeId,
            ResourceTypeName = training.ResourceType?.Name,
            TrainingComplete = training.TrainingComplete,
            Confirmed = training.Confirmed?.AsUtc().ToIsoString(),
            ConfirmedById = training.ConfirmedBy,
            ConfirmedByName = NameExtensions.FullName(training.ConfirmedByUser?.FirstName, training.ConfirmedByUser?.LastName),
        };

        /// <summary>Brukeren med opplæringene som er lastet (<c>Trainings</c>).</summary>
        public static ShiftUserResponse MapShiftUser(User user) => new()
        {
            Id = user.UserId,
            PhoneNumber = user.UserName,
            FirstName = user.FirstName,
            LastName = user.LastName,
            FullName = user.FullName(),
            Trainings = user.Trainings.Select(MapTraining).ToList(),
        };
    }
}
