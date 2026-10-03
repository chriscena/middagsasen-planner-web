namespace Middagsasen.Planner.Api.Services.Authentication
{
    public interface IAuthenticationService
    {
        Task<AuthResponse> Authenticate(AuthRequest request);
        Task<OtpResponse> GenerateOtpForUser(OtpRequest request);
        Task<Actor?> GetUserBySessionId(Guid id);
        Task LogOut(Guid sessionId);
    }
}