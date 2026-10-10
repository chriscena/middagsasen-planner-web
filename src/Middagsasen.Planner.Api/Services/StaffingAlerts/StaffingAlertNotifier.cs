using Microsoft.Extensions.Options;
using Middagsasen.Planner.Api.Core;
using Middagsasen.Planner.Api.Services.Reminders;
using Middagsasen.Planner.Api.Services.Shifts;
using Middagsasen.Planner.Api.Services.SmsSender;
using Middagsasen.Planner.Api.Services.Users;

namespace Middagsasen.Planner.Api.Services.StaffingAlerts
{
    public class StaffingAlertNotifier : IStaffingAlertNotifier
    {
        public StaffingAlertNotifier(
            IStaffingAlertRepository repository,
            ISmsSender smsSender,
            IOptions<StaffingAlertOptions> options,
            TimeProvider timeProvider,
            ILogger<StaffingAlertNotifier> logger)
        {
            Repository = repository;
            SmsSender = smsSender;
            Options = options.Value;
            TimeProvider = timeProvider;
            Logger = logger;
        }

        public IStaffingAlertRepository Repository { get; }
        public ISmsSender SmsSender { get; }
        public StaffingAlertOptions Options { get; }
        public TimeProvider TimeProvider { get; }
        public ILogger<StaffingAlertNotifier> Logger { get; }

        public async Task<StaffingAlertResult> NotifyShiftWithdrawn(int userId, int resourceId, DateTime shiftStart, DateTime shiftEnd)
        {
            try
            {
                var task = await Repository.GetTask(resourceId);
                if (task is null)
                    return new StaffingAlertResult(true, 0, null);

                var nowLocal = TimeProvider.GetUtcNow().ToNorwegianLocalTime();
                var staffing = new ResourceStaffing(task.ShiftCount, task.StaffedCount);
                if (!StaffingAlertRules.IsDue(nowLocal, shiftStart, task.EndTime, staffing, Options.NoticeDays))
                    return new StaffingAlertResult(true, 0, null);

                var recipients = await Repository.GetRecipients(userId);
                if (recipients.Count == 0)
                    return new StaffingAlertResult(true, 0, null);

                var user = await Repository.GetUser(userId);
                var fullName = user.FullName();
                var shift = new ReminderShift(shiftStart, shiftEnd, task.ResourceTypeName, task.EventName);
                var openShifts = task.ShiftCount - task.StaffedCount;

                var messages = new List<SmsMessage>();
                foreach (var admin in recipients)
                {
                    // Lagrede brukernavn er normalt normalisert (8 sifre), men eldre brukere kan ha brukernavn som
                    // «admin» (deploy-skriptet beholder brukernavn det ikke kan normalisere). De kan ikke få SMS.
                    var phoneNo = admin.UserName.ToNormalizedUserName();
                    if (phoneNo == null)
                    {
                        Logger.LogWarning("Admin {AdminUserId} har ikke et gyldig telefonnummer som brukernavn og får ikke bemanningsvarsel for oppgave {ResourceId}",
                            admin.UserId, resourceId);
                        continue;
                    }
                    messages.Add(new SmsMessage
                    {
                        ReceiverPhoneNo = phoneNo.ToSmsPhoneNo(),
                        Body = StaffingAlertRules.BuildMessage(admin.FirstName, fullName, shift, openShifts),
                    });
                }

                if (messages.Count == 0)
                    return new StaffingAlertResult(true, 0, null);

                var result = await SmsSender.SendMessages(messages);
                var success = result.Success && (result.Messages?.All(m => m.Success) ?? true);

                if (success)
                {
                    Logger.LogInformation("Sendte bemanningsvarsel til {RecipientCount} admin: bruker {UserId} trakk seg fra oppgave {ResourceId}",
                        messages.Count, userId, resourceId);
                }
                else
                {
                    Logger.LogWarning("Bemanningsvarsel feilet for oppgave {ResourceId} etter at bruker {UserId} trakk seg: {Info}",
                        resourceId, userId, result.Info);
                }

                return new StaffingAlertResult(success, messages.Count, result);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Kunne ikke sende bemanningsvarsel for oppgave {ResourceId} etter at bruker {UserId} trakk seg", resourceId, userId);
                return new StaffingAlertResult(false, 0, null);
            }
        }
    }
}
