using System.ComponentModel.DataAnnotations;

namespace Middagsasen.Planner.Api.Services.Events
{
    public class MessageRequest
    {
        public const int MaxLength = 4000;

        [Required]
        [StringLength(MaxLength)]
        public string Message { get; set; } = null!;
    }
}
