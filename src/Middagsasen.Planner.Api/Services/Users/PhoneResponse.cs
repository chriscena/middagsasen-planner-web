namespace Middagsasen.Planner.Api.Services.Users
{
    public class PhoneResponse
    {
        public string PhoneNo { get; internal set; } = null!;
        public string? FirstName { get; internal set; }
        public string? LastName { get; internal set; }
        public string FullName { get; internal set; } = string.Empty;
        public int Id { get; internal set; }
    }
}