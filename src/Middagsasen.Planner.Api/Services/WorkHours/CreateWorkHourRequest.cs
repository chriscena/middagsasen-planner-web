using System.ComponentModel.DataAnnotations;

namespace Middagsasen.Planner.Api.Services.WorkHours
{
    /// <summary>
    /// Oppretter en timeføring for innlogget bruker. Eier kan ikke angis av klient.
    /// </summary>
    public class CreateWorkHourRequest
    {
        [Required]
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string? Description { get; set; }
    }
}
