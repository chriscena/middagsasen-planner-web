namespace Middagsasen.Planner.Api.Services.Reminders
{
    /// <summary>Resultatet av én kjøring av <see cref="IShiftReminderService.SendDueReminders"/>.</summary>
    /// <param name="Ran"><c>false</c> når påminnelser er av eller det er utenfor sendevinduet, så ingenting ble vurdert.</param>
    /// <param name="Sent">Antall brukere som fikk SMS.</param>
    /// <param name="Failed">Antall brukere der sendingen feilet (prøves igjen ved neste kjøring fram til <see cref="ReminderOptions.RetryUntil"/>).</param>
    public sealed record ReminderRunResult(bool Ran, int Sent, int Failed);

    public interface IShiftReminderService
    {
        /// <summary>
        /// Sender vaktpåminnelser for morgendagen til brukerne som skal ha det, hvis det er innenfor sendevinduet
        /// (<see cref="ReminderOptions.SendTime"/> til <see cref="ReminderOptions.RetryUntil"/>, norsk tid), og
        /// logger resultatet per bruker. Kaster ikke ved SMS-feil; bare feil ved lagring kastes videre.
        /// </summary>
        Task<ReminderRunResult> SendDueReminders(CancellationToken cancellationToken);
    }
}
