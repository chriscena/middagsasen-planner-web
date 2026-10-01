using Middagsasen.Planner.Api.Data;

namespace Middagsasen.Planner.Api.Services.WorkHours
{
    /// <summary>Filter på godkjenningsstatus: 1 = godkjent, 2 = avslått, 3 = åpne, null/annet = alle.</summary>
    public interface IWorkHourRepository
    {
        /// <summary>Henter føring med tracking (for endring), inkl. ApprovedByUser/ModifiedByUser.</summary>
        Task<WorkHour?> GetWorkHourById(int workHourId);

        /// <summary>Henter føring uten tracking, inkl. ApprovedByUser/ModifiedByUser.</summary>
        Task<WorkHour?> GetWorkHourByIdReadOnly(int workHourId);

        /// <summary>Paginert liste sortert på starttid synkende. <paramref name="userId"/> null = alle brukere.</summary>
        Task<(IReadOnlyList<WorkHour> Items, int TotalCount)> GetWorkHours(int? userId, int? approved, int skip, int take);

        /// <summary>Avsluttede føringer (både start og slutt satt) for summering.</summary>
        Task<IReadOnlyList<WorkHourInterval>> GetIntervals(int? userId, DateTime? startFrom = null);

        void Add(WorkHour workHour);
        void Remove(WorkHour workHour);
        Task SaveChangesAsync();
    }

    public record WorkHourInterval(int UserId, int? ApprovalStatus, DateTime StartTime, DateTime EndTime)
    {
        public double Hours => (EndTime - StartTime).TotalHours;
    }
}
