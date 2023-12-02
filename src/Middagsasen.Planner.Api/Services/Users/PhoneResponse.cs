namespace Middagsasen.Planner.Api.Services.Users
{
    public class PhoneResponse
    {
        public string PhoneNo { get; internal set; }
        public string? FirstName { get; internal set; }
        public string? LastName { get; internal set; }
        public string FullName { get; internal set; }
        public int Id { get; internal set; }
    }
}