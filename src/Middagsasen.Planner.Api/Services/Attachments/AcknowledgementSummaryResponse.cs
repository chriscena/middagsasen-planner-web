namespace Middagsasen.Planner.Api.Services.Attachments
{
    /// <summary>
    /// Kvitteringstall for én aktiv instruks (<c>GET api/resourcetypes/{id}/files/acknowledgements</c>). Populasjonen er den
    /// samme som i <see cref="AcknowledgementReportResponse"/> med <c>all=false</c>: aktive brukere med vakt på vakttypen
    /// fra og med inneværende sesongstart.
    /// </summary>
    public class AcknowledgementSummaryResponse
    {
        public int FileId { get; set; }

        /// <summary>Antall i populasjonen som har kvittert.</summary>
        public int AcknowledgedCount { get; set; }

        /// <summary>Antall brukere i populasjonen.</summary>
        public int Total { get; set; }
    }
}
