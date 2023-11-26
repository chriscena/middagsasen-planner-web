using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services.Users;

namespace Middagsasen.Planner.Api.Services
{
    public interface IUserService
    {
        Task<OtpResponse> GenerateOtpForUser(OtpRequest request);
        Task<IEnumerable<UserResponse>> GetUsers();
        Task<UserResponse?> GetUserById(int id);
        Task<UserResponse?> GetUserBySessionId(Guid id);
        Task<AuthResponse> Authenticate(AuthRequest request);
    }
}