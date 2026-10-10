using Microsoft.Extensions.Options;
using Middagsasen.Planner.Api.Core;
using Middagsasen.Planner.Api.Services.Reminders;
using Middagsasen.Planner.Api.Services.Shifts;
using Middagsasen.Planner.Api.Services.SmsSender;

namespace Middagsasen.Planner.Api.Services.StaffingAlerts
{
    public class StaffingAlertNotifier : IStaffingAlertNotifier
    {
        public StaffingAlertNotifier(
            IStaffingAlertRepository repository,
            ISmsFanOut smsFanOut,
            IOptions<StaffingAlertOptions> options,
            TimeProvider timeProvider,
            ILogger<StaffingAlertNotifier> logger)
        {
            Repository = repository;
            SmsFanOut = smsFanOut;
            Options = options.Value;
            TimeProvider = timeProvider;
            Logger = logger;
        }

        public IStaffingAlertRepository Repository { get; }
        public ISmsFanOut SmsFanOut { get; }
        public StaffingAlertOptions Options { get; }
        public TimeProvider TimeProvider { get; }
        public ILogger<StaffingAlertNotifier> Logger { get; }

        public async Task<SmsFanOutResult> NotifyShiftWithdrawn(int userId, int resourceId, DateTime shiftStart, DateTime shiftEnd)
        {
            try
            {
                var task = await Repository.GetTask(resourceId);
                if (task is null)
                    return SmsFanOutResult.Nothing;

                var nowLocal = TimeProvider.GetUtcNow().ToNorwegianLocalTime();
                if (!StaffingAlertRules.IsDue(nowLocal, shiftStart, shiftEnd, task.EndTime, task.Staffing, Options.NoticeDays))
                    return SmsFanOutResult.Nothing;

                var recipients = await Repository.GetRecipients(userId);
                if (recipients.Count == 0)
                    return SmsFanOutResult.Nothing;

                var fullName = (await Repository.GetName(userId)).FullName();
                var shift = new ReminderShift(shiftStart, shiftEnd, task.ResourceTypeName, task.EventName);
                var openShifts = ShiftRules.OpenShifts(task.Staffing);

                return await SmsFanOut.SendToUsers(
                    recipients,
                    admin => StaffingAlertRules.BuildMessage(admin.FirstName, fullName, shift, openShifts),
                    $"bemanningsvarsel for oppgave {resourceId} etter at bruker {userId} trakk seg");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Kunne ikke sende bemanningsvarsel for oppgave {ResourceId} etter at bruker {UserId} trakk seg", resourceId, userId);
                return SmsFanOutResult.Failed;
            }
        }
    }
}
