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

    /// <summary>Hva aktøren kan gjøre med en føring akkurat nå. Brukes til flaggene i svaret.</summary>
    public readonly record struct WorkHourPermissions(bool CanEdit, bool CanDelete, bool CanApprove, bool CanResetStatus)
    {
        /// <summary>Ingen handlinger tillatt (f.eks. for en føring som nettopp er slettet).</summary>
        public static WorkHourPermissions None => default;
    }

    /// <summary>
    /// Tilgangsregler for timeføringer. Ren og uten avhengigheter.
    /// Alle vurderinger gjøres mot føringens tilstand FØR en eventuell endring.
    /// Både håndhevelsen i servicen og flaggene i svaret (<see cref="GetPermissions"/>) bruker disse reglene.
    /// </summary>
    public static class WorkHourPolicy
    {
        public static bool IsOpen(WorkHour entry) => !entry.ApprovalStatus.HasValue;

        public static bool IsOwner(WorkHour entry, int userId) => entry.UserId == userId;

        /// <summary>Admin kan lese alt; vanlig bruker kun egne føringer.</summary>
        public static bool CanRead(WorkHour entry, Actor actor)
            => actor.IsAdminOrSelf(entry.UserId);

        /// <summary>
        /// Redigere innhold (starttid, sluttid, beskrivelse).
        /// Eier eller admin, og kun når føringen er åpen.
        /// </summary>
        public static WorkHourAccess CanEdit(WorkHour entry, Actor actor)
        {
            if (!actor.IsAdminOrSelf(entry.UserId)) return WorkHourAccess.Forbidden;
            return IsOpen(entry) ? WorkHourAccess.Allowed : WorkHourAccess.Locked;
        }

        /// <summary>Slette. Samme regel som redigering.</summary>
        public static WorkHourAccess CanDelete(WorkHour entry, Actor actor) => CanEdit(entry, actor);

        /// <summary>
        /// Sette status. Kun admin. Godkjenne/avslå kun åpne føringer;
        /// «Ingen status» (null) kun på godkjente/avslåtte føringer.
        /// Vurderer kun tilgang og tilstand, ikke om statusverdien er gyldig: enhver ikke-null verdi
        /// behandles som godkjenn/avslå. Servicen validerer verdien etter policyen, slik at
        /// tilgang (403) og låsing (409) vurderes før ugyldig verdi (400).
        /// </summary>
        public static WorkHourAccess CanSetStatus(WorkHour entry, Actor actor, ApprovalStatus? newStatus)
        {
            if (!actor.IsAdmin) return WorkHourAccess.Forbidden;

            if (newStatus is null)
                return IsOpen(entry) ? WorkHourAccess.Locked : WorkHourAccess.Allowed;

            return IsOpen(entry) ? WorkHourAccess.Allowed : WorkHourAccess.Locked;
        }

        /// <summary>Flaggene i svaret, beregnet med nøyaktig de samme reglene som håndheves.</summary>
        public static WorkHourPermissions GetPermissions(WorkHour entry, Actor actor) => new(
            CanEdit: CanEdit(entry, actor) == WorkHourAccess.Allowed,
            CanDelete: CanDelete(entry, actor) == WorkHourAccess.Allowed,
            // Godkjenne og avslå har samme regel.
            CanApprove: CanSetStatus(entry, actor, ApprovalStatus.Approved) == WorkHourAccess.Allowed,
            CanResetStatus: CanSetStatus(entry, actor, null) == WorkHourAccess.Allowed);
    }
}
