using Microsoft.EntityFrameworkCore;
using Middagsasen.Planner.Api.Data;

namespace Middagsasen.Planner.Api.Services.StaffingAlerts
{
    public class StaffingAlertRepository : IStaffingAlertRepository
    {
        public StaffingAlertRepository(PlannerDbContext dbContext)
        {
            DbContext = dbContext;
        }

        public PlannerDbContext DbContext { get; }

        public async Task<User> GetUser(int userId)
        {
            return await DbContext.Users.AsNoTracking().SingleAsync(u => u.UserId == userId);
        }

        public async Task<TaskStaffing?> GetTask(int resourceId)
        {
            return await DbContext.EventResource
                .AsNoTracking()
                .Where(r => r.EventResourceId == resourceId)
                .Select(r => new TaskStaffing(
                    r.ResourceType.Name,
                    r.Event.Name,
                    r.StartTime,
                    r.EndTime,
                    r.ShiftCount,
                    r.Shifts.Count()))
                .SingleOrDefaultAsync();
        }

        public async Task<IReadOnlyList<User>> GetRecipients(int excludeUserId)
        {
            return await DbContext.Users
                .AsNoTracking()
                .Where(u => u.IsAdmin && u.StaffingAlerts && !u.Inactive && u.UserId != excludeUserId)
                .ToListAsync();
        }
    }
}
