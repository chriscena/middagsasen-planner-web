using Middagsasen.Planner.Api.Core;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services.Events;
using Middagsasen.Planner.Api.Services.ResourceTypes;

namespace Middagsasen.Planner.Api.Services.Resources
{
    /// <summary>
    /// Mapping uten flagg for innlogget bruker. Bare <see cref="ResourceReader"/> bruker den, og den kjenner til hvilke
    /// navigasjoner som må være lastet.
    /// </summary>
    internal static class ResourceMapping
    {
        /// <summary>Krever <c>Trainers.User</c>, <c>Files.CreatedByUser</c> og <c>Files.UpdatedByUser</c>.</summary>
        public static ResourceTypeResponse MapResourceType(ResourceType resourceType) => new()
        {
            Id = resourceType.ResourceTypeId,
            Name = resourceType.Name,
            DefaultShiftCount = resourceType.DefaultShiftCount,
            NotificationMessage = resourceType.NotificationMessage,
            HasTraining = resourceType.Trainers.Count > 0,
            Trainers = resourceType.Trainers.OrderBy(t => t.ResourceTypeTrainerId).Select(MapTrainer).ToList(),
            Files = resourceType.Files.OrderBy(f => f.ResourceTypeFileId).Select(MapFile).ToList(),
        };

        private static ResourceTypeTrainerResponse MapTrainer(ResourceTypeTrainer trainer) => new()
        {
            Id = trainer.ResourceTypeTrainerId,
            UserId = trainer.UserId,
            FullName = trainer.User.FullName(),
            PhoneNo = trainer.User.UserName,
        };

        /// <summary>Krever <c>CreatedByUser</c> og <c>UpdatedByUser</c>.</summary>
        public static FileInfoResponse MapFile(ResourceTypeFile file) => new()
        {
            Id = file.ResourceTypeFileId,
            ResourceTypeId = file.ResourceTypeId,
            FileName = file.FileName,
            Description = file.Description,
            MimeType = file.MimeType,
            Created = file.Created.AsUtc().ToIsoString(),
            CreatedBy = NameExtensions.FullName(file.CreatedByUser?.FirstName, file.CreatedByUser?.LastName),
            Updated = file.Updated.AsUtc().ToIsoString(),
            UpdatedBy = NameExtensions.FullName(file.UpdatedByUser?.FirstName, file.UpdatedByUser?.LastName),
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
