namespace Middagsasen.Planner.Api.Data
{
    /// <summary>
    /// Godkjenningsstatus for en timeføring. Ingen status (null) betyr at føringen er åpen (ubehandlet).
    /// Verdiene lagres som heltall i databasen (WorkHours.ApprovalStatus) og må ikke endres.
    /// </summary>
    public enum ApprovalStatus
    {
        /// <summary>Godkjent.</summary>
        Approved = 1,
        /// <summary>Avslått.</summary>
        Rejected = 2,
    }
}
