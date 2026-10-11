using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace Middagsasen.Planner.Api.Services.Attachments
{
    /// <summary>
    /// Laste opp et vedlegg på en vakttype (<c>POST api/resourcetypes/{id}/files</c>, multipart). Bindes fra skjemafeltene
    /// <c>file</c>, <c>description</c>, <c>requiresAcknowledgement</c> og <c>isMandatory</c>.
    /// </summary>
    public class FileUploadRequest
    {
        // Navnene på skjemafeltene settes eksplisitt (camelCase), slik frontend sender dem og OpenAPI-dokumentet beskriver dem.
        [FromForm(Name = "file")]
        public IFormFile File { get; set; } = null!;

        [FromForm(Name = "description")]
        [Required, StringLength(1000)]
        public string Description { get; set; } = null!;

        /// <summary>Vedlegget er en instruks: brukerne skal kvittere for å ha lest den.</summary>
        [FromForm(Name = "requiresAcknowledgement")]
        public bool RequiresAcknowledgement { get; set; }

        /// <summary>Instruksen er påkrevd for å ta vakt. Krever <see cref="RequiresAcknowledgement"/>.</summary>
        [FromForm(Name = "isMandatory")]
        public bool IsMandatory { get; set; }
    }
}
