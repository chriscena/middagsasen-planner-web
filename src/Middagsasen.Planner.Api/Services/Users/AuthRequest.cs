namespace Middagsasen.Planner.Api.Services.Users
{
    public class AuthRequest
    {
        public string UserName { get; set; } = null!;
        public string Password { get; set; } = null!;
    }
}