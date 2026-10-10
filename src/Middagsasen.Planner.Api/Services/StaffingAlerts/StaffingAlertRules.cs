using Middagsasen.Planner.Api.Services.Reminders;
using Middagsasen.Planner.Api.Services.Shifts;

namespace Middagsasen.Planner.Api.Services.StaffingAlerts
{
    /// <summary>
    /// Reglene for bemanningsvarsel (issue #43): når en bruker trekker seg fra en vakt slik at oppgaven mangler
    /// bemanning, og vakta starter om to dager eller mindre, får admin med varselet på en SMS. Ren klasse uten I/O,
    /// så den kan testes uten database. Alle tider er norsk lokal tid uten sone.
    /// </summary>
    public static class StaffingAlertRules
    {
        /// <summary>
        /// Hvor mange kalenderdager fram i tid vakta høyst kan starte for at varselet sendes. I dag er 0, i morgen 1,
        /// så med 2 varsles det for i dag, i morgen og i overmorgen.
        /// </summary>
        public const int NoticeDays = 2;

        /// <summary>
        /// Om bemanningsvarsel skal sendes etter at en vakt er fjernet. Vurderes med ferske data etter commit:
        /// <list type="bullet">
        /// <item>oppgaven mangler bemanning etter fjerningen (<see cref="ShiftRules.IsMissingStaff(ResourceStaffing)"/>);
        /// var den overbooket og fortsatt full, sendes ingenting,</item>
        /// <item>oppgaven er ikke avsluttet (<paramref name="resourceEnd"/> er etter <paramref name="nowLocal"/>),</item>
        /// <item>vaktas effektive start er høyst <see cref="NoticeDays"/> kalenderdager fram i tid. I dag og i morgen teller
        /// også, og det gjør en vakt som allerede har startet så lenge oppgaven ikke er avsluttet.</item>
        /// </list>
        /// </summary>
        /// <param name="nowLocal">Nå, norsk lokal tid.</param>
        /// <param name="shiftStart">Vaktas effektive start (vaktens egne tider, ellers oppgavens; se <see cref="ShiftReminderRules.EffectivePeriod"/>).</param>
        /// <param name="resourceEnd">Oppgavens slutt.</param>
        /// <param name="staffing">Bemanningen på oppgaven etter fjerningen.</param>
        public static bool IsDue(DateTime nowLocal, DateTime shiftStart, DateTime resourceEnd, ResourceStaffing staffing)
        {
            if (!ShiftRules.IsMissingStaff(staffing))
                return false;
            if (resourceEnd <= nowLocal)
                return false;

            var daysUntilStart = DateOnly.FromDateTime(shiftStart).DayNumber - DateOnly.FromDateTime(nowLocal).DayNumber;
            return daysUntilStart <= NoticeDays;
        }

        /// <summary>
        /// Meldingsteksten, f.eks. «Hei Ola! Kari Nordmann har trukket seg fra vakt tirsdag 14.10: 18–22 storheis (Diskokveld).
        /// Oppgaven har nå 1 ledig vakt.» Dag og vakt formateres som i vaktpåminnelsen (<see cref="ShiftReminderRules.FormatDay"/>
        /// og <see cref="ShiftReminderRules.FormatShift"/>). Flertall gir «2 ledige vakter». Uten fornavn blir hilsenen «Hei!».
        /// </summary>
        /// <param name="adminFirstName">Fornavnet til mottakeren, kan mangle.</param>
        /// <param name="userFullName">Fullt navn på den som trakk seg.</param>
        /// <param name="shift">Vakta som ble fjernet, med effektive tider.</param>
        /// <param name="openShifts">Antall ledige vakter på oppgaven etter fjerningen (<c>ShiftCount - StaffedCount</c>).</param>
        public static string BuildMessage(string? adminFirstName, string userFullName, ReminderShift shift, int openShifts)
        {
            var greeting = string.IsNullOrWhiteSpace(adminFirstName) ? "Hei!" : $"Hei {adminFirstName.Trim()}!";
            var open = openShifts == 1 ? "1 ledig vakt" : $"{openShifts} ledige vakter";
            return $"{greeting} {userFullName} har trukket seg fra vakt {ShiftReminderRules.FormatDay(DateOnly.FromDateTime(shift.Start))}: "
                + $"{ShiftReminderRules.FormatShift(shift)}. Oppgaven har nå {open}.";
        }
    }
}
