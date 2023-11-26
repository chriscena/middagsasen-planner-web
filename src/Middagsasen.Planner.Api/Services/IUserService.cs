using Middagsasen.Planner.Api.Services.Users;

namespace Middagsasen.Planner.Api.Services
{
    public interface IUserService
    {
        Task<IEnumerable<UserResponse>> GetUsers();
        Task<UserResponse?> GetUserById(int id);
    }
}