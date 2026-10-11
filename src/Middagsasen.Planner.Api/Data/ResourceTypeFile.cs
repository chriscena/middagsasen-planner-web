namespace Middagsasen.Planner.Api.Data
{
    /// <summary>
    /// Vedlegg: en fil (PDF eller bilde) admin har lagt på en vakttype. Med <see cref="RequiresAcknowledgement"/> er
    /// vedlegget en instruks, og med <see cref="IsMandatory"/> i tillegg en påkrevd instruks. Vedlegg slettes aldri
    /// fysisk, de arkiveres (<see cref="Archived"/>).
    /// </summary>
    public class ResourceTypeFile
    {
        public int ResourceTypeFileId { get; set; }
        public string StorageName { get; set; } = null!;
        public string FileName { get; set; } = null!;
        public string Description { get; set; } = null!;
        public string MimeType { get; set; } = null!;
        public int ResourceTypeId { get; set; }
        public DateTime Created { get; set; }
        public int CreatedBy { get; set; }
        public DateTime Updated { get; set; }
        public int UpdatedBy { get; set; }

        /// <summary>Vedlegget er en instruks: brukerne skal kvittere for å ha lest den.</summary>
        public bool RequiresAcknowledgement { get; set; }

        /// <summary>
        /// Instruksen er påkrevd: brukeren må ha kvittert før egen påmelding på vakttypen. Kan bare være sann når
        /// <see cref="RequiresAcknowledgement"/> er sann.
        /// </summary>
        public bool IsMandatory { get; set; }

        /// <summary>Når vedlegget ble arkivert (UTC), eller <c>null</c> for et aktivt vedlegg.</summary>
        public DateTime? Archived { get; set; }
        public int? ArchivedBy { get; set; }

        public ResourceType ResourceType { get; set; } = null!;
        public User CreatedByUser { get; set; } = null!;
        public User UpdatedByUser { get; set; } = null!;
        public User? ArchivedByUser { get; set; }
        public virtual ICollection<ResourceTypeFileAcknowledgement> Acknowledgements { get; set; } = new HashSet<ResourceTypeFileAcknowledgement>();
    }
}
