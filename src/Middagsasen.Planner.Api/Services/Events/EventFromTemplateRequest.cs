using System.Text.Json.Serialization;
using Middagsasen.Planner.Api.Core;

namespace Middagsasen.Planner.Api.Services.Events
{
    public class EventFromTemplateRequest
    {
        /// <summary>Dagen vaktlista legges på. JSON: <c>"yyyy-MM-dd"</c>. År utenfor 1900–9998 avvises.</summary>
        [JsonConverter(typeof(LocalDateConverter))]
        public required DateOnly StartDate { get; set; }
    }
}