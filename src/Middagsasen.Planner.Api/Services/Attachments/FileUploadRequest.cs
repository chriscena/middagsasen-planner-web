namespace Middagsasen.Planner.Api.Services.Attachments
{
    /// <summary>Laste opp et vedlegg på en vakttype (<c>POST api/resourcetypes/{id}/files</c>, multipart).</summary>
    public class FileUploadRequest
    {
        public IFormFile FileInfo { get; set; } = null!;
        public string Description { get; set; } = null!;

        /// <summary>Vedlegget er en instruks: brukerne skal kvittere for å ha lest den.</summary>
        public bool RequiresAcknowledgement { get; set; }

        /// <summary>Instruksen er påkrevd for å ta vakt. Krever <see cref="RequiresAcknowledgement"/>.</summary>
        public bool IsMandatory { get; set; }
    }
}
