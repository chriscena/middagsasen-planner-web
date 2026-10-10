using Middagsasen.Planner.Api.Services.SmsSender;

namespace Middagsasen.Planner.Api.Services.StaffingAlerts
{
    /// <summary>
    /// Sender bemanningsvarsel på SMS til admin som har slått det på. Skal kalles <b>etter</b> at fjerningen av vakta
    /// er lagret (commit), slik at varselet vurderes mot ferske tall og aldri sendes for en endring som ble rullet tilbake.
    /// </summary>
    public interface IStaffingAlertNotifier
    {
        /// <summary>
        /// Varsler admin om at <paramref name="userId"/> har trukket seg fra en vakt på oppgaven, hvis
        /// <see cref="StaffingAlertRules.IsDue"/> sier at det er aktuelt. Den som trakk seg varsles aldri. Admin hvis
        /// brukernavn ikke er et gyldig telefonnummer (f.eks. «admin») hoppes over med en advarsel i loggen; de andre
        /// varsles likevel (<see cref="ISmsFanOut"/>). Kaster ikke ved SMS-feil: feilen logges og returneres i
        /// <see cref="SmsFanOutResult"/>; var varselet ikke aktuelt, eller ingen admin har det på, er resultatet
        /// <see cref="SmsFanOutResult.Nothing"/>.
        /// </summary>
        /// <param name="userId">Brukeren som trakk seg.</param>
        /// <param name="resourceId">Oppgaven vakta sto på.</param>
        /// <param name="shiftStart">Vaktas effektive start (norsk lokal tid).</param>
        /// <param name="shiftEnd">Vaktas effektive slutt (norsk lokal tid).</param>
        Task<SmsFanOutResult> NotifyShiftWithdrawn(int userId, int resourceId, DateTime shiftStart, DateTime shiftEnd);
    }
}
