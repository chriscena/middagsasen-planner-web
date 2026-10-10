namespace Middagsasen.Planner.Api.Services.Users
{
    /// <summary>
    /// Feltene en innlogget bruker selv kan endre via <c>PUT api/me</c>.
    /// Felt som ikke er satt (eller er tomme) blir ikke endret.
    /// Inneholder bevisst ikke IsAdmin eller PhoneNo – de kan kun endres av administrator.
    /// Typen er selve tilgangsregelen for egen bruker: en bruker kan bare endre seg selv via denne
    /// requesten, og øvrige brukerendepunkter krever <c>[Authorize(Role = Roles.Administrator)]</c>.
    /// Derfor finnes det ingen egen UserPolicy (se issue #96).
    /// </summary>
    public class UpdateMeRequest
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Password { get; set; }
        /// <summary>Skjul brukeren fra telefonlisten. Endres kun når verdien er satt.</summary>
        public bool? IsHidden { get; set; }
        /// <summary>SMS-påminnelse dagen før vakt. Endres kun når verdien er satt.</summary>
        public bool? ShiftReminders { get; set; }
        /// <summary>Bemanningsvarsel (SMS til admin når noen trekker seg fra vakt). Endres kun når verdien er satt. Bare admin kan slå det på.</summary>
        public bool? StaffingAlerts { get; set; }
    }
}
