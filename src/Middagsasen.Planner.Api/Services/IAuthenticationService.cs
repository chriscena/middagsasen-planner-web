using Middagsasen.Planner.Api.Services.Authentication;
using Middagsasen.Planner.Api.Services.Users;

namespace Middagsasen.Planner.Api.Services
{
    public interface IAuthenticationService
    {
        Task<AuthResponse> Authenticate(AuthRequest request);
        Task<OtpResponse> GenerateOtpForUser(OtpRequest request);
        Task<UserResponse?> GetUserBySessionId(Guid id);
        Task LogOut(Guid sessionId);
    }
}