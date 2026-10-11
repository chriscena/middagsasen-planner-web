using System.ComponentModel.DataAnnotations;

namespace Middagsasen.Planner.Api.Services.Attachments
{
    /// <summary>Kvittere for en instruks (<c>POST api/resourcetypes/{id}/files/{fileId}/acknowledgements</c>).</summary>
    public class AcknowledgeRequest
    {
        /// <summary>Valgfri kommentar, maks 1000 tegn. Tom streng lagres som <c>null</c>.</summary>
        [StringLength(1000)]
        public string? Comment { get; set; }
    }
}
