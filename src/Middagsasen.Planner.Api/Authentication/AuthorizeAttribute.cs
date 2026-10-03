using Microsoft.AspNetCore.Mvc.Filters;
using Middagsasen.Planner.Api.Services;
using Middagsasen.Planner.Api.Services.Users;

namespace Middagsasen.Planner.Api.Authentication
{
    /// <summary>
    /// Krever innlogget bruker, og eventuelt en bestemt rolle. Kaster exceptions i stedet for å sette
    /// <c>context.Result</c>, slik at <see cref="ExceptionHandlingMiddleware"/> skriver svaret som ProblemDetails.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class AuthorizeAttribute : Attribute, IAuthorizationFilter
    {
        public string? Role { get; set; }

        /// <exception cref="NotAuthenticatedException">Ingen innlogget bruker (gir 401).</exception>
        /// <exception cref="ForbiddenAccessException">Brukeren mangler påkrevd rolle (gir 403).</exception>
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var user = (UserResponse?)context.HttpContext.Items["User"];
            if (user == null)
                throw new NotAuthenticatedException();

            if (!string.IsNullOrWhiteSpace(Role) && !context.HttpContext.User.IsInRole(Role))
                throw new ForbiddenAccessException();
        }
    }

    public class Roles
    {
        public const string Administrator = "Administrator";
        public const string User = "User";  
    }
}
