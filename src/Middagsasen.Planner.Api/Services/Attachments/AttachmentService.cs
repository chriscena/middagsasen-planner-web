using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Core;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services.Storage;

namespace Middagsasen.Planner.Api.Services.Attachments
{
    public class AttachmentService : IAttachmentService
    {
        internal const string FileNotFoundMessage = "Fant ikke vedlegget.";
        internal const string ResourceTypeNotFoundMessage = "Fant ikke vakttypen.";
        internal const string MandatoryRequiresAcknowledgementMessage = "En instruks kan bare være påkrevd når den krever kvittering.";
        internal const string ArchivedMessage = "Vedlegget er arkivert.";
        internal const string NotAnInstructionMessage = "Vedlegget krever ikke kvittering.";

        public AttachmentService(IAttachmentRepository repository, IStorageService storage, ICurrentUserService currentUser, TimeProvider timeProvider)
        {
            Repository = repository;
            Storage = storage;
            CurrentUser = currentUser;
            TimeProvider = timeProvider;
        }

        public IAttachmentRepository Repository { get; }
        public IStorageService Storage { get; }
        public ICurrentUserService CurrentUser { get; }
        public TimeProvider TimeProvider { get; }

        // --- Vedlegg (admin) ---

        public async Task<FileInfoResponse> Upload(int resourceTypeId, FileUploadRequest request)
        {
            var actor = CurrentUser.ToActor();
            if (!AttachmentPolicy.CanManage(actor))
                throw new ForbiddenAccessException();
            ValidateFlags(request.RequiresAcknowledgement, request.IsMandatory);
            // Sjekkes før bloben lagres, så en ukjent vakttype ikke etterlater en foreldreløs fil i lagringen.
            if (!await Repository.ResourceTypeExists(resourceTypeId))
                throw new EntityNotFoundException(ResourceTypeNotFoundMessage);

            var now = TimeProvider.GetUtcNow().UtcDateTime;
            var storageName = Guid.NewGuid().ToString();
            await Storage.Save($"{GetContainerPath(now)}/{storageName}", request.File.OpenReadStream());

            var file = await Repository.Add(new ResourceTypeFile
            {
                StorageName = storageName,
                FileName = request.File.FileName,
                Description = request.Description,
                MimeType = request.File.ContentType,
                ResourceTypeId = resourceTypeId,
                RequiresAcknowledgement = request.RequiresAcknowledgement,
                IsMandatory = request.IsMandatory,
                Created = now,
                CreatedBy = actor.UserId,
                Updated = now,
                UpdatedBy = actor.UserId,
            });

            // Hentes på nytt for navnene på opprettet/endret av.
            var saved = await Repository.Get(file.ResourceTypeFileId, resourceTypeId) ?? throw new EntityNotFoundException(FileNotFoundMessage);
            return AttachmentMapping.MapFile(saved);
        }

        public async Task<FileInfoResponse> UpdateMetadata(int resourceTypeId, int fileId, FileMetadataRequest request)
        {
            var actor = CurrentUser.ToActor();
            if (!AttachmentPolicy.CanManage(actor))
                throw new ForbiddenAccessException();
            ValidateFlags(request.RequiresAcknowledgement, request.IsMandatory);

            var file = await Repository.Get(fileId, resourceTypeId) ?? throw new EntityNotFoundException(FileNotFoundMessage);
            if (file.Archived != null)
                throw new DomainValidationException(ArchivedMessage);

            file.Description = request.Description;
            file.RequiresAcknowledgement = request.RequiresAcknowledgement;
            file.IsMandatory = request.IsMandatory;
            file.Updated = TimeProvider.GetUtcNow().UtcDateTime;
            file.UpdatedBy = actor.UserId;
            await Repository.SaveChangesAsync();

            var saved = await Repository.Get(fileId, resourceTypeId) ?? throw new EntityNotFoundException(FileNotFoundMessage);
            return AttachmentMapping.MapFile(saved, await AcknowledgedAt(fileId, actor.UserId));
        }

        public async Task Archive(int resourceTypeId, int fileId)
        {
            var actor = CurrentUser.ToActor();
            if (!AttachmentPolicy.CanManage(actor))
                throw new ForbiddenAccessException();

            var file = await Repository.Get(fileId, resourceTypeId) ?? throw new EntityNotFoundException(FileNotFoundMessage);
            if (file.Archived != null)
                return;

            // Blob-en beholdes: arkiverte vedlegg kan fortsatt lastes ned av admin.
            file.Archived = TimeProvider.GetUtcNow().UtcDateTime;
            file.ArchivedBy = actor.UserId;
            await Repository.SaveChangesAsync();
        }

        public async Task<FileResponse> Download(int resourceTypeId, int fileId)
        {
            var actor = CurrentUser.ToActor();
            var file = await Repository.Get(fileId, resourceTypeId) ?? throw new EntityNotFoundException(FileNotFoundMessage);
            // Arkiverte vedlegg finnes ikke for vanlige brukere, så svaret er 404 og ikke 403.
            if (!AttachmentPolicy.CanDownload(actor, file.Archived != null))
                throw new EntityNotFoundException(FileNotFoundMessage);

            var data = await Storage.Read($"{GetContainerPath(file.Created)}/{file.StorageName}");
            return new FileResponse { Data = data, FileName = file.FileName, MimeType = file.MimeType };
        }

        public async Task<IEnumerable<FileInfoResponse>> GetArchived(int resourceTypeId)
        {
            if (!AttachmentPolicy.CanManage(CurrentUser.ToActor()))
                throw new ForbiddenAccessException();

            var files = await Repository.GetArchived(resourceTypeId);
            return files.Select(f => AttachmentMapping.MapFile(f)).ToList();
        }

        // --- Instrukser og kvitteringer ---

        public async Task<FileInfoResponse> Acknowledge(int resourceTypeId, int fileId, AcknowledgeRequest? request)
        {
            var actor = CurrentUser.ToActor();
            var file = await Repository.Get(fileId, resourceTypeId) ?? throw new EntityNotFoundException(FileNotFoundMessage);
            if (!AttachmentPolicy.CanAcknowledge(file))
                throw new DomainValidationException(file.Archived != null ? ArchivedMessage : NotAnInstructionMessage);

            // Idempotent: en kvittering endres aldri, så en ny på samme fil returnerer den eksisterende.
            var acknowledgement = await Repository.GetAcknowledgement(fileId, actor.UserId)
                ?? await Repository.Acknowledge(new ResourceTypeFileAcknowledgement
                {
                    ResourceTypeFileId = fileId,
                    UserId = actor.UserId,
                    Acknowledged = TimeProvider.GetUtcNow().UtcDateTime,
                    Comment = string.IsNullOrWhiteSpace(request?.Comment) ? null : request.Comment.Trim(),
                });

            return AttachmentMapping.MapFile(file, acknowledgement.Acknowledged);
        }

        public async Task<IEnumerable<InstructionGroupResponse>> GetMyInstructions(int userId)
        {
            var instructions = await Repository.GetActiveInstructions();
            if (instructions.Count == 0)
                return [];

            var acknowledged = await Repository.GetAcknowledgedFileIds(userId);
            var appliesTo = await Repository.GetResourceTypesWithShiftsSince(userId, AttachmentRules.SeasonStartUtc(TimeProvider.GetUtcNow()));

            return instructions
                .GroupBy(f => f.ResourceTypeId)
                .Select(g => new InstructionGroupResponse
                {
                    ResourceTypeId = g.Key,
                    ResourceTypeName = g.First().ResourceType.Name,
                    AppliesToMe = AttachmentRules.AppliesTo(g.Key, appliesTo),
                    Files = g
                        .OrderBy(f => f.Description, StringComparer.OrdinalIgnoreCase).ThenBy(f => f.ResourceTypeFileId)
                        .Select(f => AttachmentMapping.MapFile(f, acknowledged.TryGetValue(f.ResourceTypeFileId, out var at) ? at : null))
                        .ToList(),
                })
                .OrderByDescending(g => g.AppliesToMe)
                .ThenBy(g => g.ResourceTypeName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(g => g.ResourceTypeId)
                .ToList();
        }

        public async Task<AcknowledgementReportResponse> GetAcknowledgementReport(int resourceTypeId, int fileId, bool all)
        {
            var actor = CurrentUser.ToActor();
            if (!AttachmentPolicy.CanViewReport(actor))
                throw new ForbiddenAccessException();

            var file = await Repository.Get(fileId, resourceTypeId) ?? throw new EntityNotFoundException(FileNotFoundMessage);
            var acknowledgements = (await Repository.GetAcknowledgements(fileId)).ToDictionary(a => a.UserId);
            var appliesTo = await Repository.GetUsersWithShiftsSince(resourceTypeId, AttachmentRules.SeasonStartUtc(TimeProvider.GetUtcNow()));

            // all=false filtreres i databasen til brukerne vakttypen gjelder for.
            var population = await Repository.GetActiveUsers(all ? null : appliesTo);
            var users = population
                .Select(u =>
                {
                    var acknowledgement = acknowledgements.GetValueOrDefault(u.UserId);
                    return new AcknowledgementReportUserResponse
                    {
                        UserId = u.UserId,
                        FullName = u.FullName(),
                        AcknowledgedAt = AttachmentMapping.ToIsoString(acknowledgement?.Acknowledged),
                        Comment = acknowledgement?.Comment,
                        AppliesTo = appliesTo.Contains(u.UserId),
                    };
                })
                .OrderBy(u => u.AcknowledgedAt is not null)
                .ThenBy(u => u.FullName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(u => u.UserId)
                .ToList();

            return new AcknowledgementReportResponse
            {
                File = AttachmentMapping.MapFile(file, acknowledgements.GetValueOrDefault(actor.UserId)?.Acknowledged),
                Total = users.Count,
                AcknowledgedCount = users.Count(u => u.AcknowledgedAt is not null),
                Users = users,
            };
        }

        public async Task<IEnumerable<AcknowledgementSummaryResponse>> GetAcknowledgementSummaries(int resourceTypeId)
        {
            if (!AttachmentPolicy.CanViewReport(CurrentUser.ToActor()))
                throw new ForbiddenAccessException();
            if (!await Repository.ResourceTypeExists(resourceTypeId))
                throw new EntityNotFoundException(ResourceTypeNotFoundMessage);

            return await Repository.GetAcknowledgementSummaries(resourceTypeId, AttachmentRules.SeasonStartUtc(TimeProvider.GetUtcNow()));
        }

        // --- Hjelpere ---

        private static void ValidateFlags(bool requiresAcknowledgement, bool isMandatory)
        {
            if (isMandatory && !requiresAcknowledgement)
                throw new DomainValidationException(MandatoryRequiresAcknowledgementMessage);
        }

        private async Task<DateTime?> AcknowledgedAt(int fileId, int userId)
            => (await Repository.GetAcknowledgement(fileId, userId))?.Acknowledged;

        /// <summary>Mappen i lagringen, etter opplastingsmåneden. Må gi samme sti ved nedlasting som ved opplasting.</summary>
        internal static string GetContainerPath(DateTime created) => $"resourceTypes/{created.Year}/{created.Month:0#}";
    }
}
