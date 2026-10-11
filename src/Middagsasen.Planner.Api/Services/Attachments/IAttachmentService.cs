namespace Middagsasen.Planner.Api.Services.Attachments
{
    /// <summary>
    /// Vedlegg på vakttyper: opplasting, metadata, arkivering og nedlasting, og instrukser med kvittering, instruksliste
    /// og kvitteringsrapport. Tilgang avgjøres av <see cref="AttachmentPolicy"/>, reglene av <see cref="AttachmentRules"/>.
    /// </summary>
    public interface IAttachmentService
    {
        /// <summary>Laster opp et vedlegg på vakttypen (kun admin). Filen lagres først når vakttypen finnes og flaggene er gyldige.</summary>
        /// <exception cref="ForbiddenAccessException">Innlogget bruker er ikke admin.</exception>
        /// <exception cref="EntityNotFoundException">Vakttypen finnes ikke.</exception>
        /// <exception cref="DomainValidationException"><c>IsMandatory</c> uten <c>RequiresAcknowledgement</c>.</exception>
        Task<FileInfoResponse> Upload(int resourceTypeId, FileUploadRequest request);

        /// <summary>Endrer beskrivelse og flagg (kun admin). Kvitteringene beholdes når flaggene skrus av og på.</summary>
        /// <exception cref="ForbiddenAccessException">Innlogget bruker er ikke admin.</exception>
        /// <exception cref="EntityNotFoundException">Vedlegget finnes ikke på vakttypen.</exception>
        /// <exception cref="DomainValidationException"><c>IsMandatory</c> uten <c>RequiresAcknowledgement</c>, eller vedlegget er arkivert.</exception>
        Task<FileInfoResponse> UpdateMetadata(int resourceTypeId, int fileId, FileMetadataRequest request);

        /// <summary>
        /// Arkiverer vedlegget (kun admin): setter <c>Archived</c>/<c>ArchivedBy</c>. Kvitteringene og filen i lagringen
        /// beholdes. Idempotent for et allerede arkivert vedlegg.
        /// </summary>
        /// <exception cref="ForbiddenAccessException">Innlogget bruker er ikke admin.</exception>
        /// <exception cref="EntityNotFoundException">Vedlegget finnes ikke på vakttypen.</exception>
        Task Archive(int resourceTypeId, int fileId);

        /// <summary>Laster ned vedlegget. Arkiverte vedlegg finnes bare for admin.</summary>
        /// <exception cref="EntityNotFoundException">Vedlegget finnes ikke, eller er arkivert og brukeren ikke er admin.</exception>
        Task<FileResponse> Download(int resourceTypeId, int fileId);

        /// <summary>Arkiverte vedlegg på vakttypen, nyeste først (kun admin).</summary>
        /// <exception cref="ForbiddenAccessException">Innlogget bruker er ikke admin.</exception>
        Task<IEnumerable<FileInfoResponse>> GetArchived(int resourceTypeId);

        /// <summary>
        /// Kvitterer for instruksen for innlogget bruker. Idempotent: finnes kvitteringen, returneres den eksisterende
        /// uendret (også kommentaren). Svaret har <see cref="FileInfoResponse.AcknowledgedAt"/> satt.
        /// </summary>
        /// <param name="request">Valgfri kommentar; <c>null</c> (ingen body) gir kvittering uten kommentar.</param>
        /// <exception cref="EntityNotFoundException">Vedlegget finnes ikke på vakttypen.</exception>
        /// <exception cref="DomainValidationException">Vedlegget krever ikke kvittering, eller er arkivert.</exception>
        Task<FileInfoResponse> Acknowledge(int resourceTypeId, int fileId, AcknowledgeRequest? request);

        /// <summary>
        /// Alle aktive instrukser gruppert per vakttype, med <c>appliesToMe</c> og kvitteringsstatus for brukeren.
        /// Vakttyper som gjelder brukeren først, deretter etter navn. Vakttyper uten instrukser utelates.
        /// </summary>
        Task<IEnumerable<InstructionGroupResponse>> GetMyInstructions(int userId);

        /// <summary>
        /// Kvitteringsrapport for instruksen (kun admin). <paramref name="all"/> = <c>false</c> gir brukerne vakttypen
        /// gjelder for, <c>true</c> alle aktive brukere.
        /// </summary>
        /// <exception cref="ForbiddenAccessException">Innlogget bruker er ikke admin.</exception>
        /// <exception cref="EntityNotFoundException">Vedlegget finnes ikke på vakttypen.</exception>
        Task<AcknowledgementReportResponse> GetAcknowledgementReport(int resourceTypeId, int fileId, bool all);

        /// <summary>
        /// Kvitteringstall for hver aktive instruks på vakttypen (kun admin), med samme populasjon og telling som
        /// <see cref="GetAcknowledgementReport"/> med <c>all=false</c>. Arkiverte vedlegg og vedlegg uten kvittering utelates.
        /// </summary>
        /// <exception cref="ForbiddenAccessException">Innlogget bruker er ikke admin.</exception>
        /// <exception cref="EntityNotFoundException">Vakttypen finnes ikke.</exception>
        Task<IEnumerable<AcknowledgementSummaryResponse>> GetAcknowledgementSummaries(int resourceTypeId);
    }
}
