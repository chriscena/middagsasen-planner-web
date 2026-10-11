namespace Middagsasen.Planner.Api.Data
{
    /// <summary>
    /// Kvittering: at en bruker har bekreftet å ha lest og forstått en instruks (<see cref="ResourceTypeFile"/> med
    /// <see cref="ResourceTypeFile.RequiresAcknowledgement"/>). Gjelder den konkrete filen, så en ny versjon av instruksen
    /// krever ny kvittering. En kvittering endres aldri; høyst én per bruker og fil.
    /// </summary>
    public class ResourceTypeFileAcknowledgement
    {
        public int ResourceTypeFileAcknowledgementId { get; set; }
        public int ResourceTypeFileId { get; set; }
        public int UserId { get; set; }

        /// <summary>Når brukeren kvitterte (UTC).</summary>
        public DateTime Acknowledged { get; set; }

        /// <summary>Valgfri kommentar fra brukeren, maks 1000 tegn.</summary>
        public string? Comment { get; set; }

        public ResourceTypeFile File { get; set; } = null!;
        public User User { get; set; } = null!;
    }
}
