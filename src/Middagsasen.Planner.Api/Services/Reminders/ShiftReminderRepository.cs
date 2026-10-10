using Microsoft.EntityFrameworkCore;
using Middagsasen.Planner.Api.Data;

namespace Middagsasen.Planner.Api.Services.Reminders
{
    public class ShiftReminderRepository : IShiftReminderRepository
    {
        public ShiftReminderRepository(PlannerDbContext dbContext)
        {
            DbContext = dbContext;
        }

        public PlannerDbContext DbContext { get; }

        public async Task<IReadOnlyList<ReminderCandidate>> GetCandidates(DateOnly shiftDate)
        {
            // Vaktens egne tider kan avvike fra oppgavens, så vi henter vakter på oppgaver dagen før til og med dagen
            // etter, og avgjør presist i minnet med de effektive tidene.
            var from = shiftDate.AddDays(-1).ToDateTime(TimeOnly.MinValue);
            var to = shiftDate.AddDays(2).ToDateTime(TimeOnly.MinValue);

            var rows = await DbContext.Shifts
                .AsNoTracking()
                .Where(s => s.User.ShiftReminders && !s.User.Inactive)
                .Where(s => s.Resource.StartTime >= from && s.Resource.StartTime < to)
                .Where(s => !s.User.SentShiftReminders.Any(r => r.ShiftDate == shiftDate && r.Success))
                .Select(s => new
                {
                    s.UserId,
                    s.User.UserName,
                    s.User.FirstName,
                    s.StartTime,
                    s.EndTime,
                    ResourceStart = s.Resource.StartTime,
                    ResourceEnd = s.Resource.EndTime,
                    ResourceTypeName = s.Resource.ResourceType.Name,
                    EventName = s.Resource.Event.Name,
                })
                .ToListAsync();

            var byUser = rows
                .Select(r => new
                {
                    r.UserId,
                    r.UserName,
                    r.FirstName,
                    Period = ShiftReminderRules.EffectivePeriod(r.StartTime, r.EndTime, r.ResourceStart, r.ResourceEnd),
                    r.ResourceTypeName,
                    r.EventName,
                })
                .Where(r => DateOnly.FromDateTime(r.Period.Start) == shiftDate)
                .GroupBy(r => r.UserId)
                .ToList();

            if (byUser.Count == 0)
                return [];

            var userIds = byUser.Select(g => g.Key).ToList();
            var failed = await DbContext.ShiftReminders
                .Where(r => r.ShiftDate == shiftDate && !r.Success && userIds.Contains(r.UserId))
                .ToDictionaryAsync(r => r.UserId);

            return byUser
                .Select(g => new ReminderCandidate(
                    g.Key,
                    g.First().UserName,
                    g.First().FirstName,
                    g.Select(r => new ReminderShift(r.Period.Start, r.Period.End, r.ResourceTypeName, r.EventName)).ToList(),
                    failed.GetValueOrDefault(g.Key)))
                .ToList();
        }

        public void Add(ShiftReminder reminder) => DbContext.ShiftReminders.Add(reminder);

        public Task SaveChangesAsync() => DbContext.SaveChangesAsync();
    }
}
