using Middagsasen.Planner.Api.Services;

namespace Middagsasen.Planner.Api.Authentication
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        /// <summary>Satt av <see cref="JwtMiddleware"/> når forespørselen har en gyldig sesjon.</summary>
        public Actor? Actor => _httpContextAccessor.HttpContext?.Items["User"] as Actor?;

        public int UserId => Actor?.UserId ?? throw new NotAuthenticatedException();

        public bool IsAdmin => Actor?.IsAdmin ?? false;
    }
}
