using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Middagsasen.Planner.Api.Services.Users;
using System.Security.Claims;

namespace Middagsasen.Planner.Api.Authentication
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class AuthorizeAttribute : Attribute, IAuthorizationFilter
    {
        public string? Role { get; set; }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var user = (UserResponse?)context.HttpContext.Items["User"];
            if (user == null)
            {
                context.Result = new UnauthorizedResult();
            }
            else if (!string.IsNullOrWhiteSpace(Role) && !context.HttpContext.User.IsInRole(Role))
            {
                context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);  
            }
        }
    }

    public class Roles
    {
        public const string Administrator = "Administrator";
        public const string User = "User";  
    }
}
