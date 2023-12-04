namespace Middagsasen.Planner.Api.Services.Users
{
    public class UserRequest
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Password { get; set; }
        public string? PhoneNo { get; set; }
        public bool? IsAdmin { get; set; }
        public bool? IsHidden { get; set; }
    }
}