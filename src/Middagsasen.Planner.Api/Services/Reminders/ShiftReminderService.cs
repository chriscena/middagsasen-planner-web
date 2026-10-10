using Microsoft.Extensions.Options;
using Middagsasen.Planner.Api.Core;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services.SmsSender;
using Middagsasen.Planner.Api.Services.Users;

namespace Middagsasen.Planner.Api.Services.Reminders
{
    /// <summary>
    /// Vaktpåminnelse: én SMS per bruker per vaktdag, dagen før. Reglene ligger i <see cref="ShiftReminderRules"/>,
    /// utvelgelsen i <see cref="IShiftReminderRepository"/>. Alle SMS-ene i en kjøring sendes i ett kall til
    /// <see cref="ISmsSender"/>, og resultatet skrives per bruker til <c>ShiftReminders</c>.
    /// </summary>
    public class ShiftReminderService : IShiftReminderService
    {
        public ShiftReminderService(
            IShiftReminderRepository repository,
            ISmsSender smsSender,
            IOptions<ReminderOptions> options,
            TimeProvider timeProvider,
            ILogger<ShiftReminderService> logger)
        {
            Repository = repository;
            SmsSender = smsSender;
            Options = options.Value;
            TimeProvider = timeProvider;
            Logger = logger;
        }

        public IShiftReminderRepository Repository { get; }
        public ISmsSender SmsSender { get; }
        public ReminderOptions Options { get; }
        public TimeProvider TimeProvider { get; }
        public ILogger<ShiftReminderService> Logger { get; }

        public async Task<ReminderRunResult> SendDueReminders(CancellationToken cancellationToken)
        {
            var nowLocal = TimeProvider.GetUtcNow().ToNorwegianLocalTime();
            if (!Options.Enabled || !ShiftReminderRules.IsSendWindowOpen(Options, nowLocal))
                return new ReminderRunResult(false, 0, 0);

            var shiftDate = ShiftReminderRules.ShiftDateFor(nowLocal);
            var candidates = await Repository.GetCandidates(shiftDate);
            if (candidates.Count == 0)
                return new ReminderRunResult(true, 0, 0);

            var messages = candidates.Select(c => new SmsMessage
            {
                ReceiverPhoneNo = c.UserName.ToSmsPhoneNo(),
                Body = ShiftReminderRules.BuildMessage(c.FirstName, shiftDate, c.Shifts),
            }).ToList();

            var outcomes = await Send(messages, shiftDate);

            var sentTime = TimeProvider.GetUtcNow().UtcDateTime;
            var sent = 0;
            var failed = 0;
            foreach (var (candidate, message) in candidates.Zip(messages))
            {
                var (success, info) = outcomes(message.ReceiverPhoneNo);
                if (success) sent++; else failed++;

                var reminder = candidate.FailedReminder;
                if (reminder == null)
                {
                    reminder = new ShiftReminder { UserId = candidate.UserId, ShiftDate = shiftDate };
                    Repository.Add(reminder);
                }
                reminder.SentTime = sentTime;
                reminder.Success = success;
                reminder.Info = success ? null : info;
            }

            // En samtidig kjøring som allerede har sendt for samme bruker og dag gir brudd på den unike indeksen her.
            // Det kastes videre, så bakgrunnsjobben logger det.
            await Repository.SaveChangesAsync();

            if (failed == 0)
                Logger.LogInformation("Sendte vaktpåminnelse for {ShiftDate} til {Sent} brukere", shiftDate, sent);
            else
                Logger.LogWarning("Vaktpåminnelse for {ShiftDate}: {Sent} sendt, {Failed} feilet (prøves igjen fram til kl. {RetryUntil})",
                    shiftDate, sent, failed, Options.RetryUntil);

            return new ReminderRunResult(true, sent, failed);
        }

        /// <summary>
        /// Sender alle meldingene i ett kall og returnerer et oppslag fra telefonnummer til (suksess, info).
        /// Mangler svar per melding, gjelder svaret for hele kallet alle. Kaster SMS-tjenesten, feiler alle.
        /// </summary>
        private async Task<Func<long, (bool Success, string? Info)>> Send(List<SmsMessage> messages, DateOnly shiftDate)
        {
            SmsResult result;
            try
            {
                result = await SmsSender.SendMessages(messages);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "SMS-tjenesten feilet ved sending av vaktpåminnelse for {ShiftDate}", shiftDate);
                return _ => (false, ex.Message);
            }

            var perMessage = (result.Messages ?? [])
                .GroupBy(m => m.ReceiverPhoneNo)
                .ToDictionary(g => g.Key, g => g.First());

            return phoneNo =>
            {
                var message = perMessage.GetValueOrDefault(phoneNo);
                var success = result.Success && (message?.Success ?? true);
                return (success, success ? null : message?.Info ?? result.Info);
            };
        }
    }
}
