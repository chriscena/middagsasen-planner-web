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
    /// <remarks>
    /// <b>Reservasjon før sending (høyst én SMS).</b> Loggradene lagres i to steg: først reserveres én rad per
    /// kandidat med <c>Success = false</c> og <see cref="SendingInfo"/>, <i>før</i> noe sendes. Feiler den lagringen
    /// (f.eks. den unike indeksen fordi en samtidig kjøring allerede har en rad for samme bruker og dag), kastes
    /// feilen videre og ingen SMS sendes. Deretter sendes SMS-ene, og radene oppdateres med det faktiske utfallet.
    /// Feiler <i>den andre</i> lagringen, står radene igjen som feilet og sendes på nytt ved neste kjøring — det er
    /// det eneste vinduet for en dobbel SMS, og det er smalt (en forbigående databasefeil akkurat etter sendingen).
    /// Brukere uten gyldig telefonnummer som brukernavn (f.eks. «admin») får en rad med <see cref="InvalidPhoneNoInfo"/>
    /// og ingen SMS; de andre sendes som normalt.
    /// </remarks>
    public class ShiftReminderService : IShiftReminderService
    {
        /// <summary>Info på loggraden mens sendingen pågår (reservasjonen). Står igjen hvis lagringen etter sending feiler.</summary>
        public const string SendingInfo = "Sending pågår";

        /// <summary>Info på loggraden til en bruker hvis brukernavn ikke er et gyldig telefonnummer.</summary>
        public const string InvalidPhoneNoInfo = "Ugyldig telefonnummer";

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
            var candidates = await Repository.GetCandidates(shiftDate, cancellationToken);
            if (candidates.Count == 0)
                return new ReminderRunResult(true, 0, 0);

            var sentTime = TimeProvider.GetUtcNow().UtcDateTime;
            var sent = 0;
            var failed = 0;

            // Steg 1: reserver én loggrad per kandidat før noe sendes. En samtidig kjøring som allerede har en rad for
            // samme bruker og dag gir brudd på den unike indeksen her, og da sendes ingenting.
            var pending = new List<(ShiftReminder Reminder, SmsMessage Message)>();
            foreach (var candidate in candidates)
            {
                var reminder = candidate.FailedReminder;
                if (reminder == null)
                {
                    reminder = new ShiftReminder { UserId = candidate.UserId, ShiftDate = shiftDate };
                    Repository.Add(reminder);
                }
                reminder.SentTime = sentTime;
                reminder.Success = false;

                var phoneNo = candidate.UserName.ToNormalizedUserName();
                if (phoneNo == null)
                {
                    Logger.LogWarning("Bruker {UserId} har ikke et gyldig telefonnummer som brukernavn og får ikke vaktpåminnelse for {ShiftDate}",
                        candidate.UserId, shiftDate);
                    reminder.Info = InvalidPhoneNoInfo;
                    failed++;
                    continue;
                }

                reminder.Info = SendingInfo;
                pending.Add((reminder, new SmsMessage
                {
                    ReceiverPhoneNo = phoneNo.ToSmsPhoneNo(),
                    Body = ShiftReminderRules.BuildMessage(candidate.FirstName, shiftDate, candidate.Shifts),
                }));
            }
            await Repository.SaveChangesAsync(cancellationToken);

            // Steg 2: send, og skriv utfallet. Feiler denne lagringen, står radene som feilet og sendes på nytt neste gang.
            if (pending.Count > 0)
            {
                var outcomes = await Send(pending.Select(p => p.Message).ToList(), shiftDate);
                foreach (var (reminder, message) in pending)
                {
                    var (success, info) = outcomes(message.ReceiverPhoneNo);
                    if (success) sent++; else failed++;
                    reminder.Success = success;
                    reminder.Info = success ? null : info;
                }
                await Repository.SaveChangesAsync(cancellationToken);
            }

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
