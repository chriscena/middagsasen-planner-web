namespace Middagsasen.Planner.Api.Services.Users
{
    public class UserResponse
    {
        public int Id { get; internal set; }
        public string UserName { get; internal set; } = null!;
        public string? FirstName { get; internal set; }
        public string? LastName { get; internal set; }
        public string? FullName { get; internal set; }
        public bool IsAdmin { get; internal set; }
    }
}