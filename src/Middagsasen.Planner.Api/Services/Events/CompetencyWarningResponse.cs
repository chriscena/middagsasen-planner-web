namespace Middagsasen.Planner.Api.Services.Events
{
    public class CompetencyWarningResponse
    {
        public string CompetencyName { get; internal set; } = null!;
        public int MinimumRequired { get; internal set; }
        public int CurrentCount { get; internal set; }
    }
}
