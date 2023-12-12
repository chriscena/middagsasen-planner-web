namespace Middagsasen.Planner.Api.Services.Users
{
    public interface IUserService
    {
        Task<IEnumerable<UserResponse>> GetUsers();
        Task<UserResponse?> GetUserById(int id);
        Task<IEnumerable<PhoneResponse>> GetPhoneList();
        Task<UserResponse> Create(UserRequest request);
        Task<UserResponse?> Update(int id, UserRequest request);
        Task<UserResponse?> Delete(int id);
        Task<HallOfFameResponse> GetHallOfFame();
    }
}