using Middagsasen.Planner.Api.Data;

namespace Middagsasen.Planner.Api.Services.Competencies
{
    /// <summary>
    /// Felles regler for brukerkompetanser. Ren og uten avhengigheter: «nå» sendes inn.
    /// <see cref="UserCompetency.ExpiryDate"/> sammenlignes med <paramref name="utcNow"/> i UTC, slik den alltid er blitt.
    /// </summary>
    public static class CompetencyRules
    {
        /// <summary>
        /// Kompetansen er utløpt: den har en utløpsdato, og utløpsdatoen er nådd (<c>ExpiryDate &lt;= nå</c>).
        /// </summary>
        public static bool IsExpired(UserCompetency userCompetency, DateTime utcNow)
            => userCompetency.ExpiryDate is { } expiry && expiry <= utcNow;

        /// <summary>
        /// Kompetansen er gyldig: godkjent og ikke utløpt (<c>Approved &amp;&amp; (ExpiryDate == null || ExpiryDate &gt; nå)</c>).
        /// Brukes både for kompetanseadvarsler på vakter og for <see cref="UserCompetencyResponse.IsExpired"/>.
        /// </summary>
        public static bool IsValid(UserCompetency userCompetency, DateTime utcNow)
            => userCompetency.Approved && !IsExpired(userCompetency, utcNow);
    }
}
