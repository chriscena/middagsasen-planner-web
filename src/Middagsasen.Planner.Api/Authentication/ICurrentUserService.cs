using Middagsasen.Planner.Api.Services.Users;

namespace Middagsasen.Planner.Api.Authentication
{
    public interface ICurrentUserService
    {
        UserResponse? User { get; }
        int UserId { get; }
        bool IsAdmin { get; }
    }
}
