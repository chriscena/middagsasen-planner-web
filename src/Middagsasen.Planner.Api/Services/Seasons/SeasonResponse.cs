namespace Middagsasen.Planner.Api.Services.Seasons
{
    public class SeasonResponse
    {
        /// <summary>Sesongens startår, f.eks. 2024 for sesongen 2024/2025.</summary>
        public int StartYear { get; set; }
        public string Label { get; set; } = "";
        public bool IsCurrent { get; set; }
    }
}
