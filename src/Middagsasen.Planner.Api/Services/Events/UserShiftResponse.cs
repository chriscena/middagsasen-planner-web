namespace Middagsasen.Planner.Api.Services.Events
{
    public class ShiftSeasonResponse 
    {
        public required string Label { get; set; }
        public IEnumerable<UserShiftResponse> Shifts { get; set; } = [];
    }

    public class UserShiftResponse
    {
        public int Id { get; set; }
        public string? StartDate { get; set; }
        public string? StartTime { get; set; }
        public string? EndTime { get; set; }
        public string? ResourceName { get; set; }
        public string? Comment { get; set;}
        public string Season { get; set; } = null!;
    }
}