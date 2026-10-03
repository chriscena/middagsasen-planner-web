using Middagsasen.Planner.Api.Services.Events;

namespace Middagsasen.Planner.Api.Services.Shifts
{
    /// <summary>
    /// Vaktpåmelding: ta vakt, endre, sette opplæring og trekke seg. Reglene ligger i <see cref="ShiftRules"/>.
    /// Alle skriveoperasjonene returnerer hele ressursen etter endringen, med flagg for innlogget bruker.
    /// </summary>
    public interface IShiftService
    {
        /// <summary>
        /// Setter opp en bruker (innlogget bruker hvis <see cref="SignUpRequest.UserId"/> er <c>null</c>) på ressursen.
        /// Har ressurstypen opplæring og brukeren ikke svart før, må <see cref="SignUpRequest.NeedsTraining"/> sendes med.
        /// </summary>
        /// <exception cref="EntityNotFoundException">Ressursen eller brukeren finnes ikke.</exception>
        /// <exception cref="ForbiddenAccessException">Ikke-admin setter opp en annen bruker.</exception>
        /// <exception cref="DomainValidationException">Full, avsluttet, duplikat, ugyldige tider eller manglende opplæringssvar.</exception>
        Task<ShiftResult> SignUp(int resourceId, SignUpRequest request);

        /// <summary>
        /// Endrer tider, kommentar og (for admin) eier av vakta. Flyttes vakta til en bruker uten opplæringsrad på en
        /// ressurstype med opplæring, må <see cref="ChangeShiftRequest.NeedsTraining"/> sendes med (som ved påmelding).
        /// </summary>
        Task<ShiftResult> Change(int shiftId, ChangeShiftRequest request);

        /// <summary>Setter opplæringen til eieren av vakta på ressursens ressurstype (eier, trener eller admin).</summary>
        Task<ShiftResult> SetTraining(int shiftId, SetTrainingRequest request);

        /// <summary>Trekker eieren fra vakta (sletter den). En vakt med registrerte timer kan ikke slettes (400).</summary>
        Task<ShiftResult> Withdraw(int shiftId);

        /// <summary>
        /// Endrer minimum bemanning på ressursen (kun admin), under ressurslåsen siden kapasitetsreglene avhenger av den.
        /// Returnerer ressursen etter endringen med flagg for innlogget bruker.
        /// </summary>
        /// <exception cref="EntityNotFoundException">Ressursen finnes ikke.</exception>
        /// <exception cref="ForbiddenAccessException">Innlogget bruker er ikke admin.</exception>
        /// <exception cref="DomainValidationException">Negativ minimum bemanning.</exception>
        Task<ResourceResponse> SetMinimumStaff(int resourceId, MinimumStaffRequest request);

        /// <summary>
        /// Lager en mapper som gir ressurser med flagg for innlogget bruker. Brukes av lesesiden (EventsService),
        /// slik at <c>GET api/events</c> og skriveoperasjonene gir like flagg.
        /// </summary>
        Task<ResourceMapper> CreateResourceMapper();
    }
}
