namespace Middagsasen.Planner.Api.Services.Users
{
    /// <summary>
    /// Feltene en innlogget bruker selv kan endre via <c>PUT api/me</c>.
    /// Felt som ikke er satt (eller er tomme) blir ikke endret.
    /// Inneholder bevisst ikke IsAdmin eller PhoneNo – de kan kun endres av administrator.
    /// </summary>
    public class UpdateMeRequest
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Password { get; set; }
        /// <summary>Skjul brukeren fra telefonlisten. Endres kun når verdien er satt.</summary>
        public bool? IsHidden { get; set; }
    }
}
