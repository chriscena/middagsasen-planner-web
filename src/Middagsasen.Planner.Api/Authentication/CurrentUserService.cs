using Middagsasen.Planner.Api.Services.Users;

namespace Middagsasen.Planner.Api.Authentication
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public UserResponse? User =>
            (UserResponse?)_httpContextAccessor.HttpContext?.Items["User"];

        public int UserId => User?.Id ?? throw new UnauthorizedAccessException();

        public bool IsAdmin => User?.IsAdmin ?? false;
    }
}
