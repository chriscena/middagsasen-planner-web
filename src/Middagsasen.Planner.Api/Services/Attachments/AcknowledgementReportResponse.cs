namespace Middagsasen.Planner.Api.Services.Attachments
{
    /// <summary>
    /// Kvitteringsrapport for én instruks (<c>GET api/resourcetypes/{id}/files/{fileId}/acknowledgements</c>): hvem i
    /// populasjonen som har kvittert. Populasjonen er brukerne vakttypen gjelder for (har vakt på den fra og med
    /// inneværende sesongstart), eller alle aktive brukere med <c>all=true</c>.
    /// </summary>
    public class AcknowledgementReportResponse
    {
        public FileInfoResponse File { get; set; } = null!;

        /// <summary>Antall brukere i populasjonen.</summary>
        public int Total { get; set; }

        /// <summary>Antall i populasjonen som har kvittert.</summary>
        public int AcknowledgedCount { get; set; }

        /// <summary>Brukerne i populasjonen, ikke kvittert først, deretter etter etternavn og fornavn.</summary>
        public IEnumerable<AcknowledgementReportUserResponse> Users { get; set; } = new List<AcknowledgementReportUserResponse>();
    }

    /// <summary>En bruker i kvitteringsrapporten.</summary>
    public class AcknowledgementReportUserResponse
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = null!;

        /// <summary>Når brukeren kvitterte, UTC-tidspunkt med sone (<c>yyyy-MM-ddTHH:mm:ssZ</c>), eller <c>null</c> hvis brukeren ikke har kvittert.</summary>
        public string? AcknowledgedAt { get; set; }
        public string? Comment { get; set; }

        /// <summary>Vakttypen gjelder brukeren: brukeren har vakt på den fra og med inneværende sesongstart.</summary>
        public bool AppliesTo { get; set; }
    }
}
