namespace Middagsasen.Planner.Api.Services.Users
{
    public class HallOfFameResponse
    {
        public IEnumerable<HallOfFamerResponse> HallOfFamers { get; set; } = new List<HallOfFamerResponse>();
    }
}