using Middagsasen.Planner.Api.Core;
using Middagsasen.Planner.Api.Data;

namespace Middagsasen.Planner.Api.Services.Attachments
{
    /// <summary>Mapping av vedlegg til <see cref="FileInfoResponse"/>. Brukes av <see cref="AttachmentService"/> og lesemodulen for oppgaver.</summary>
    internal static class AttachmentMapping
    {
        /// <summary>Krever <c>CreatedByUser</c> og <c>UpdatedByUser</c>.</summary>
        /// <param name="acknowledgedAt">Når innlogget bruker kvitterte, eller <c>null</c> (ikke kvittert, eller ukjent bruker).</param>
        public static FileInfoResponse MapFile(ResourceTypeFile file, DateTime? acknowledgedAt = null) => new()
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
            RequiresAcknowledgement = file.RequiresAcknowledgement,
            IsMandatory = file.IsMandatory,
            AcknowledgedAt = acknowledgedAt.AsUtc(),
            Archived = file.Archived.AsUtc(),
        };
    }
}
