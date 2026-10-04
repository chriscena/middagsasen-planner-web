namespace Middagsasen.Planner.Api.Services.WorkHours
{
    /// <summary>
    /// Filter på godkjenningsstatus i lister over timeføringer.
    /// Verdiene er en del av URL-ene (<c>?approved=3</c>) og må ikke endres.
    /// </summary>
    public enum ApprovalFilter
    {
        /// <summary>Alle føringer (samme som å utelate filteret).</summary>
        All = 0,
        /// <summary>Kun godkjente føringer.</summary>
        Approved = 1,
        /// <summary>Kun avslåtte føringer.</summary>
        Rejected = 2,
        /// <summary>Kun ubehandlede (åpne) føringer.</summary>
        Pending = 3,
    }
}
