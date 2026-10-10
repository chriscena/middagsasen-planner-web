using Microsoft.EntityFrameworkCore;
using Middagsasen.Planner.Api.Core;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services.Shifts;
using Middagsasen.Planner.Api.Services.SmsSender;

namespace Middagsasen.Planner.Api.Services.StaffingAlerts
{
    public class StaffingAlertRepository : IStaffingAlertRepository
    {
        public StaffingAlertRepository(PlannerDbContext dbContext)
        {
            DbContext = dbContext;
        }

        public PlannerDbContext DbContext { get; }

        public async Task<PersonName> GetName(int userId)
        {
            return await DbContext.Users
                .AsNoTracking()
                .Where(u => u.UserId == userId)
                .Select(u => new PersonName(u.FirstName, u.LastName))
                .SingleAsync();
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
                    // Bemannede vakter telles som i IShiftRepository.GetStaffing (ShiftRepository): alle vakter på oppgaven.
                    new ResourceStaffing(r.ShiftCount, r.Shifts.Count())))
                .SingleOrDefaultAsync();
        }

        public async Task<IReadOnlyList<SmsRecipient>> GetRecipients(int excludeUserId)
        {
            return await DbContext.Users
                .AsNoTracking()
                .Where(u => u.IsAdmin && u.StaffingAlerts && !u.Inactive && u.UserId != excludeUserId)
                .Select(u => new SmsRecipient(u.UserId, u.UserName, u.FirstName))
                .ToListAsync();
        }
    }
}
