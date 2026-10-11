namespace Middagsasen.Planner.Api.Services.Attachments
{
    /// <summary>Et vedlegg på en vakttype (uten innhold). Med <see cref="RequiresAcknowledgement"/> er det en instruks.</summary>
    public class FileInfoResponse
    {
        public int Id { get; set; }
        public int ResourceTypeId { get; set; }
        public string FileName { get; set; } = null!;
        /// <summary>Beskrivelsen, som vises som navn på vedlegget.</summary>
        public string Description { get; set; } = null!;
        /// <summary>Når filen ble lastet opp, UTC-tidspunkt med sone (<c>yyyy-MM-ddTHH:mm:ssZ</c>).</summary>
        public string Created { get; set; } = null!;
        public string CreatedBy { get; set; } = null!;
        /// <summary>Når filen sist ble endret, UTC-tidspunkt med sone (<c>yyyy-MM-ddTHH:mm:ssZ</c>).</summary>
        public string Updated { get; set; } = null!;
        public string UpdatedBy { get; set; } = null!;
        public string MimeType { get; set; } = null!;

        /// <summary>Vedlegget er en instruks: brukerne skal kvittere for å ha lest den.</summary>
        public bool RequiresAcknowledgement { get; set; }

        /// <summary>Instruksen er påkrevd: brukeren må ha kvittert før egen påmelding på vakttypen.</summary>
        public bool IsMandatory { get; set; }

        /// <summary>
        /// Når innlogget bruker kvitterte for instruksen (UTC), eller <c>null</c> når brukeren ikke har kvittert, eller
        /// der vedlegget leses uten kjent bruker (f.eks. <c>GET api/resourcetypes</c>).
        /// </summary>
        public DateTime? AcknowledgedAt { get; set; }

        /// <summary>Når vedlegget ble arkivert (UTC), eller <c>null</c> for et aktivt vedlegg.</summary>
        public DateTime? Archived { get; set; }
    }
}
