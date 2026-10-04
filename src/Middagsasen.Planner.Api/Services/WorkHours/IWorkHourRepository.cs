using Middagsasen.Planner.Api.Data;

namespace Middagsasen.Planner.Api.Services.WorkHours
{
    public interface IWorkHourRepository
    {
        /// <summary>Henter føring med tracking (for endring), inkl. ApprovedByUser/ModifiedByUser.</summary>
        Task<WorkHour?> GetWorkHourById(int workHourId);

        /// <summary>Henter føring uten tracking, inkl. ApprovedByUser/ModifiedByUser.</summary>
        Task<WorkHour?> GetWorkHourByIdReadOnly(int workHourId);

        /// <summary>
        /// Paginert liste sortert på starttid synkende, filtrert på <paramref name="approved"/>. <paramref name="userId"/> null = alle brukere.
        /// <paramref name="from"/>/<paramref name="to"/> filtrerer på starttid i det halvåpne intervallet [from, to); null = ingen grense.
        /// </summary>
        Task<(IReadOnlyList<WorkHour> Items, int TotalCount)> GetWorkHours(int? userId, ApprovalFilter approved, DateTime? from, DateTime? to, int skip, int take);

        /// <summary>
        /// Avsluttede føringer (både start og slutt satt) for summering.
        /// <paramref name="from"/>/<paramref name="to"/> filtrerer på starttid i det halvåpne intervallet [from, to); null = ingen grense.
        /// </summary>
        Task<IReadOnlyList<WorkHourInterval>> GetIntervals(int? userId, DateTime? from = null, DateTime? to = null);

        void Add(WorkHour workHour);
        void Remove(WorkHour workHour);
        /// <summary>
        /// Lagrer endringer. Kaster <see cref="Services.EntityLockedException"/> hvis føringens
        /// status er endret av en annen siden den ble hentet (optimistisk samtidighet på ApprovalStatus).
        /// </summary>
        Task SaveChangesAsync();
    }

    public record WorkHourInterval(int UserId, ApprovalStatus? ApprovalStatus, DateTime StartTime, DateTime EndTime)
    {
        public double Hours => (EndTime - StartTime).TotalHours;
    }
}
