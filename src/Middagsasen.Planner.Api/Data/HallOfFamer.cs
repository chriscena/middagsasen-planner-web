namespace Middagsasen.Planner.Api.Data
{
    public class HallOfFamer
    {
        public int UserId { get; set; }
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
        public int Shifts { get; set; }
    }
}