namespace Middagsasen.Planner.Api.Services.Users
{
    public interface IUserService
    {
        Task<IEnumerable<UserResponse>> GetUsers();
        Task<UserResponse> GetUserById(int id);
        /// <summary>Om brukeren finnes og er aktiv (ikke <c>Inactive</c>). Skjulte brukere (<c>IsHidden</c>) regnes som aktive.</summary>
        Task<bool> IsActiveUser(int id);
        Task<IEnumerable<PhoneResponse>> GetPhoneList();
        Task<UserResponse> Create(UserRequest request);
        Task<UserResponse> Update(int id, UserRequest request);
        Task<UserResponse> UpdateMe(int userId, UpdateMeRequest request);
        Task<UserResponse> Delete(int id);
        Task<HallOfFameResponse> GetHallOfFame();
    }
}