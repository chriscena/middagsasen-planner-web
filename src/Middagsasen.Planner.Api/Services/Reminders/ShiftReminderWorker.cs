using Microsoft.Extensions.Options;

namespace Middagsasen.Planner.Api.Services.Reminders
{
    /// <summary>
    /// Bakgrunnsjobb som kaller <see cref="IShiftReminderService.SendDueReminders"/> ved oppstart og deretter hvert
    /// <see cref="ReminderOptions.PollInterval"/>. Servicen avgjør selv om det er noe å sende, så jobben er dum.
    /// Feil logges og stopper ikke løkka. Forutsetter én instans av API-et; kjører to likevel, stopper den unike
    /// indeksen på <c>ShiftReminders</c> den andre allerede ved reservasjonen, før noen SMS sendes
    /// (se <see cref="ShiftReminderService"/>).
    /// </summary>
    public class ShiftReminderWorker : BackgroundService
    {
        public ShiftReminderWorker(IServiceScopeFactory scopeFactory, IOptions<ReminderOptions> options, ILogger<ShiftReminderWorker> logger)
        {
            ScopeFactory = scopeFactory;
            Options = options.Value;
            Logger = logger;
        }

        public IServiceScopeFactory ScopeFactory { get; }
        public ReminderOptions Options { get; }
        public ILogger<ShiftReminderWorker> Logger { get; }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!Options.Enabled)
            {
                Logger.LogInformation("Vaktpåminnelse er av (Reminders:Enabled = false)");
                return;
            }

            Logger.LogInformation("Vaktpåminnelse sendes fra kl. {SendTime} (prøves igjen fram til kl. {RetryUntil}), sjekkes hvert {PollInterval}",
                Options.SendTime, Options.RetryUntil, Options.PollInterval);

            using var timer = new PeriodicTimer(Options.PollInterval);
            do
            {
                await RunOnce(stoppingToken);
            }
            while (await WaitForNextTick(timer, stoppingToken));
        }

        private async Task RunOnce(CancellationToken stoppingToken)
        {
            try
            {
                using var scope = ScopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IShiftReminderService>();
                await service.SendDueReminders(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Appen stopper.
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Vaktpåminnelse feilet, prøver igjen ved neste kjøring");
            }
        }

        private static async Task<bool> WaitForNextTick(PeriodicTimer timer, CancellationToken stoppingToken)
        {
            try
            {
                return await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }
    }
}
