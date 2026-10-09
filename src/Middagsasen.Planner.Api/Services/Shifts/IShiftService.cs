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
        /// Opplæringen (<see cref="SignUpRequest.TrainingCompleted"/>) lagres i samme transaksjon som vakta. Har ressurstypen
        /// opplæring og brukeren ikke svart før, må den sendes med.
        /// </summary>
        /// <exception cref="EntityNotFoundException">Ressursen eller brukeren finnes ikke.</exception>
        /// <exception cref="ForbiddenAccessException">Ikke-admin setter opp en annen bruker.</exception>
        /// <exception cref="DomainValidationException">Full, avsluttet, duplikat, ugyldige tider eller manglende opplæringssvar.</exception>
        Task<ShiftResult> SignUp(int resourceId, SignUpRequest request);

        /// <summary>
        /// Endrer tider, kommentar og (for admin) eier av vakta, og opplæringen til eieren etter endringen
        /// (<see cref="ChangeShiftRequest.TrainingCompleted"/>) i samme transaksjon. Flyttes vakta til en bruker uten
        /// opplæringsrad på en ressurstype med opplæring, må svaret sendes med (som ved påmelding); ellers er det valgfritt.
        /// </summary>
        Task<ShiftResult> Change(int shiftId, ChangeShiftRequest request);

        /// <summary>Setter opplæringen til eieren av vakta på ressursens ressurstype (eier, trener eller admin).</summary>
        Task<ShiftResult> SetTraining(int shiftId, SetTrainingRequest request);

        /// <summary>Trekker eieren fra vakta (sletter den).</summary>
        Task<ShiftResult> Withdraw(int shiftId);

        /// <summary>
        /// Legger til én ledig plass på ressursen (kun admin): <c>ShiftCount</c> settes til
        /// <see cref="ShiftRules.ShiftCountAfterAddingEmptySlot"/>, regnet ut under ressurslåsen.
        /// Returnerer ressursen etter endringen med flagg for innlogget bruker.
        /// </summary>
        /// <exception cref="EntityNotFoundException">Ressursen finnes ikke.</exception>
        /// <exception cref="ForbiddenAccessException">Innlogget bruker er ikke admin.</exception>
        Task<ResourceResponse> AddEmptySlot(int resourceId);

        /// <summary>
        /// Fjerner én ledig plass fra ressursen (kun admin): <c>ShiftCount</c> reduseres med én, regnet ut under
        /// ressurslåsen (<see cref="ShiftRules.ShiftCountAfterRemovingEmptySlot"/>).
        /// Returnerer ressursen etter endringen med flagg for innlogget bruker.
        /// </summary>
        /// <exception cref="EntityNotFoundException">Ressursen finnes ikke.</exception>
        /// <exception cref="ForbiddenAccessException">Innlogget bruker er ikke admin.</exception>
        /// <exception cref="DomainValidationException">Ressursen har ingen ledig plass å fjerne.</exception>
        Task<ResourceResponse> RemoveEmptySlot(int resourceId);
    }
}
