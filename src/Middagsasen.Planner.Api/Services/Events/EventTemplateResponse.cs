namespace Middagsasen.Planner.Api.Services.Events
{
    public class EventTemplateResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string EventName { get; set; } = null!;
        /// <summary>Malens start, norsk lokal tid uten sone (<c>yyyy-MM-ddTHH:mm</c>).</summary>
        public string StartTime { get; set; } = null!;
        /// <summary>Malens slutt, norsk lokal tid uten sone (<c>yyyy-MM-ddTHH:mm</c>).</summary>
        public string EndTime { get; set; } = null!;
        public IEnumerable<ResourceTemplateResponse>? ResourceTemplates { get; set; } = new List<ResourceTemplateResponse>();
        /// <summary>Anleggskravene, sortert på kompetansenavn. Kopieres til vaktlisten når den opprettes fra malen.</summary>
        public IEnumerable<CompetencyRequirementResponse> CompetencyRequirements { get; set; } = new List<CompetencyRequirementResponse>();
    }
}