using System.ComponentModel.DataAnnotations;

namespace Middagsasen.Planner.Api.Services.Attachments
{
    /// <summary>
    /// Endre beskrivelse og flagg på et vedlegg (<c>PUT api/resourcetypes/{id}/files/{fileId}</c>). Selve filen kan ikke
    /// byttes, siden kvitteringene gjelder innholdet: ny versjon er arkiver + last opp.
    /// </summary>
    public class FileMetadataRequest
    {
        [Required, StringLength(1000)]
        public string Description { get; set; } = null!;

        /// <summary>Vedlegget er en instruks: brukerne skal kvittere for å ha lest den.</summary>
        public bool RequiresAcknowledgement { get; set; }

        /// <summary>Instruksen er påkrevd for å ta vakt. Krever <see cref="RequiresAcknowledgement"/>.</summary>
        public bool IsMandatory { get; set; }
    }
}
