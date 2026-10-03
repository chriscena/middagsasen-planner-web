namespace Middagsasen.Planner.Api.Services
{
    /// <summary>
    /// Den innloggede brukeren som utfører en handling, slik tilgangspolicyene ser den.
    /// Bygges av <see cref="Authentication.ICurrentUserService"/> og sendes inn i de rene policyene,
    /// slik at de kan vurderes uten HttpContext eller databaseoppslag.
    /// </summary>
    /// <param name="UserId">Id til innlogget bruker.</param>
    /// <param name="IsAdmin">Om innlogget bruker er administrator.</param>
    public readonly record struct Actor(int UserId, bool IsAdmin)
    {
        /// <summary>Admin, eller handlingen gjelder brukeren selv.</summary>
        public bool IsAdminOrSelf(int userId) => IsAdmin || UserId == userId;
    }
}
