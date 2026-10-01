using Microsoft.EntityFrameworkCore;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services;

namespace Middagsasen.Planner.Api.Services.WorkHours
{
    public class WorkHourRepository : IWorkHourRepository
    {
        public WorkHourRepository(PlannerDbContext dbContext)
        {
            DbContext = dbContext;
        }

        public PlannerDbContext DbContext { get; }

        private IQueryable<WorkHour> WithUsers(IQueryable<WorkHour> query) => query
            .Include(w => w.ApprovedByUser)
            .Include(w => w.ModifiedByUser);

        public async Task<WorkHour?> GetWorkHourById(int workHourId)
        {
            return await WithUsers(DbContext.WorkHours)
                .SingleOrDefaultAsync(w => w.WorkHourId == workHourId);
        }

        public async Task<WorkHour?> GetWorkHourByIdReadOnly(int workHourId)
        {
            return await WithUsers(DbContext.WorkHours.AsNoTracking())
                .SingleOrDefaultAsync(w => w.WorkHourId == workHourId);
        }

        public async Task<(IReadOnlyList<WorkHour> Items, int TotalCount)> GetWorkHours(int? userId, int? approved, int skip, int take)
        {
            var query = DbContext.WorkHours.AsNoTracking();

            if (userId.HasValue)
                query = query.Where(w => w.UserId == userId.Value);

            query = approved switch
            {
                1 => query.Where(w => w.ApprovalStatus == 1),
                2 => query.Where(w => w.ApprovalStatus == 2),
                3 => query.Where(w => !w.ApprovalStatus.HasValue),
                _ => query,
            };

            var totalCount = await query.CountAsync();
            var items = await WithUsers(query)
                .OrderByDescending(w => w.StartTime)
                .ThenByDescending(w => w.WorkHourId)
                .Skip(skip)
                .Take(take)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<IReadOnlyList<WorkHourInterval>> GetIntervals(int? userId, DateTime? startFrom = null)
        {
            var query = DbContext.WorkHours
                .AsNoTracking()
                .Where(w => w.EndTime.HasValue && w.StartTime.HasValue);

            if (userId.HasValue)
                query = query.Where(w => w.UserId == userId.Value);
            if (startFrom.HasValue)
                query = query.Where(w => w.StartTime >= startFrom.Value);

            return await query
                .Select(w => new WorkHourInterval(w.UserId, w.ApprovalStatus, w.StartTime!.Value, w.EndTime!.Value))
                .ToListAsync();
        }

        public void Add(WorkHour workHour) => DbContext.WorkHours.Add(workHour);

        public void Remove(WorkHour workHour) => DbContext.WorkHours.Remove(workHour);

        public async Task SaveChangesAsync()
        {
            try
            {
                await DbContext.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // ApprovalStatus er concurrency token: føringen ble behandlet (eller slettet)
                // av en annen mellom henting og lagring.
                throw new EntityLockedException(WorkHoursService.LockedMessage, ex);
            }
        }
    }
}
