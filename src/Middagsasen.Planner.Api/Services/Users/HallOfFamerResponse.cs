namespace Middagsasen.Planner.Api.Services.Users
{
    public class HallOfFamerResponse
    {
        public int Id { get; set; }
        public string FullName { get; set; } = null!;
        public int Shifts { get; set; }
    }
}