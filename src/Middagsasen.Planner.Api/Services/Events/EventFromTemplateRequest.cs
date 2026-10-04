namespace Middagsasen.Planner.Api.Services.Events
{
    public class EventFromTemplateRequest
    {
        /// <summary>Dagen vaktlista legges på. JSON: <c>"yyyy-MM-dd"</c>.</summary>
        public required DateOnly StartDate { get; set; }
    }
}