using System.ComponentModel.DataAnnotations;

namespace Middagsasen.Planner.Api.Services.Events
{
    public class ResourceTemplateRequest
    {
        public int? Id { get; set; }
        public int ResourceTypeId { get; set; }
        /// <summary>Klokkeslett. JSON: <c>"HH:mm"</c> (sekunder kan tas med). Døgnet bestemmes ut fra vaktlista.</summary>
        public required TimeOnly StartTime { get; set; }
        /// <summary>Klokkeslett, som <see cref="StartTime"/>. Før start betyr neste døgn.</summary>
        public required TimeOnly EndTime { get; set; }
        [Range(0, int.MaxValue)]
        public required int ShiftCount { get; set; }
        public bool IsDeleted { get; set; }
    }
}