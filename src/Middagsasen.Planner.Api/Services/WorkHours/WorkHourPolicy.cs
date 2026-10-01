using Middagsasen.Planner.Api.Data;

namespace Middagsasen.Planner.Api.Services.WorkHours
{
    public enum WorkHourAccess
    {
        /// <summary>Handlingen er tillatt.</summary>
        Allowed,
        /// <summary>Brukeren har ikke tilgang (403).</summary>
        Forbidden,
        /// <summary>Brukeren har tilgang, men føringens tilstand tillater ikke handlingen (409).</summary>
        Locked,
    }

    /// <summary>
    /// Tilgangsregler for timeføringer. Ren og uten avhengigheter.
    /// Alle vurderinger gjøres mot føringens tilstand FØR en eventuell endring.
    /// </summary>
    public static class WorkHourPolicy
    {
        public const int Approved = 1;
        public const int Rejected = 2;

        public static bool IsOpen(WorkHour entry) => !entry.ApprovalStatus.HasValue;

        public static bool IsOwner(WorkHour entry, int userId) => entry.UserId == userId;

        /// <summary>Admin kan lese alt; vanlig bruker kun egne føringer.</summary>
        public static bool CanRead(WorkHour entry, bool isAdmin, int userId)
            => isAdmin || IsOwner(entry, userId);

        /// <summary>
        /// Redigere innhold (starttid, sluttid, beskrivelse) eller slette.
        /// Eier eller admin, og kun når føringen er åpen.
        /// </summary>
        public static WorkHourAccess CanEdit(WorkHour entry, bool isAdmin, int userId)
        {
            if (!isAdmin && !IsOwner(entry, userId)) return WorkHourAccess.Forbidden;
            return IsOpen(entry) ? WorkHourAccess.Allowed : WorkHourAccess.Locked;
        }

        /// <summary>
        /// Sette status. Kun admin. Godkjenne/avslå (1/2) kun åpne føringer;
        /// «Ingen status» (null) kun på godkjente/avslåtte føringer.
        /// Vurderer kun tilgang og tilstand, ikke om statusverdien er gyldig: enhver ikke-null verdi
        /// behandles som godkjenn/avslå. Servicen validerer verdien etter policyen, slik at
        /// tilgang (403) og låsing (409) vurderes før ugyldig verdi (400).
        /// </summary>
        public static WorkHourAccess CanSetStatus(WorkHour entry, bool isAdmin, int userId, int? newStatus)
        {
            if (!isAdmin) return WorkHourAccess.Forbidden;

            if (newStatus is null)
                return IsOpen(entry) ? WorkHourAccess.Locked : WorkHourAccess.Allowed;

            return IsOpen(entry) ? WorkHourAccess.Allowed : WorkHourAccess.Locked;
        }
    }
}
