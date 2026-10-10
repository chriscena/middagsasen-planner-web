using System.Globalization;
using System.Text;

namespace Middagsasen.Planner.Api.Services.Reminders
{
    /// <summary>
    /// En vakt slik den omtales i en vaktpåminnelse. Tidene er de effektive (vaktens egne, ellers oppgavens),
    /// som norsk lokal tid uten sone.
    /// </summary>
    /// <param name="Start">Effektiv start.</param>
    /// <param name="End">Effektiv slutt.</param>
    /// <param name="ResourceTypeName">Vakttypen, f.eks. «storheis».</param>
    /// <param name="EventName">Vaktlistens navn, f.eks. «Åpningstid» eller «Diskokveld».</param>
    public sealed record ReminderShift(DateTime Start, DateTime End, string ResourceTypeName, string EventName);

    /// <summary>
    /// Reglene for vaktpåminnelse: når det sendes, hvilken dag det gjelder, hvilke vakter som teller og
    /// meldingsteksten. Ren klasse uten I/O, så den kan testes uten database.
    /// </summary>
    public static class ShiftReminderRules
    {
        /// <summary>Vaktlistenavnet som ikke tas med i meldingen, siden det er det vanlige.</summary>
        public const string DefaultEventName = "Åpningstid";

        private static readonly CultureInfo Norwegian = CultureInfo.GetCultureInfo("nb-NO");

        /// <summary>
        /// Om påminnelsene kan sendes nå: fra og med <see cref="ReminderOptions.SendTime"/> til (ikke med)
        /// <see cref="ReminderOptions.RetryUntil"/>, i norsk lokal tid.
        /// </summary>
        public static bool IsSendWindowOpen(ReminderOptions options, DateTime nowLocal)
            => nowLocal.TimeOfDay >= options.SendTime && nowLocal.TimeOfDay < options.RetryUntil;

        /// <summary>Dagen påminnelsen gjelder: morgendagen i norsk lokal tid.</summary>
        public static DateOnly ShiftDateFor(DateTime nowLocal)
            => DateOnly.FromDateTime(nowLocal).AddDays(1);

        /// <summary>
        /// Vaktens effektive tider: vaktens egne når de er satt, ellers oppgavens (samme regel som
        /// <see cref="Shifts.ShiftRules.AreTimesValid"/>). En vakt hører til vaktdagen når effektiv start faller på den.
        /// </summary>
        public static (DateTime Start, DateTime End) EffectivePeriod(DateTime? shiftStart, DateTime? shiftEnd, DateTime resourceStart, DateTime resourceEnd)
            => (shiftStart ?? resourceStart, shiftEnd ?? resourceEnd);

        /// <summary>
        /// Meldingsteksten, f.eks. «Hei Kari! Kjapp påminnelse om vakt i morgen, tirsdag 14.10: 18–22 storheis, 10–14 kiosk.»
        /// Vaktene sorteres på starttid og skrives med <see cref="FormatShift"/>; dagen med <see cref="FormatDay"/>.
        /// Uten fornavn blir hilsenen «Hei!».
        /// </summary>
        public static string BuildMessage(string? firstName, DateOnly shiftDate, IReadOnlyList<ReminderShift> shifts)
        {
            var greeting = string.IsNullOrWhiteSpace(firstName) ? "Hei!" : $"Hei {firstName.Trim()}!";
            var items = shifts.OrderBy(s => s.Start).Select(FormatShift);
            return $"{greeting} Kjapp påminnelse om vakt i morgen, {FormatDay(shiftDate)}: {string.Join(", ", items)}.";
        }

        /// <summary>Dagen med norsk dagnavn i små bokstaver og dato som dag.måned, f.eks. «tirsdag 14.10».</summary>
        public static string FormatDay(DateOnly date)
            => $"{date.ToString("dddd", Norwegian).ToLower(Norwegian)} {date:dd.MM}";

        /// <summary>
        /// Én vakt i en SMS, f.eks. «18–22 storheis (Diskokveld)». Hele timer skrives uten minutter (<c>18–22</c>),
        /// ellers med (<c>18:30–22</c>). Vaktlistens navn tas med i parentes bare når det ikke er <see cref="DefaultEventName"/>.
        /// Brukes også av bemanningsvarselet, så de to meldingene omtaler vakter likt.
        /// </summary>
        public static string FormatShift(ReminderShift shift)
        {
            var text = new StringBuilder()
                .Append(FormatTime(shift.Start)).Append('–').Append(FormatTime(shift.End))
                .Append(' ').Append(shift.ResourceTypeName);
            var eventName = shift.EventName.Trim();
            if (eventName.Length > 0 && !string.Equals(eventName, DefaultEventName, StringComparison.OrdinalIgnoreCase))
                text.Append(" (").Append(eventName).Append(')');
            return text.ToString();
        }

        private static string FormatTime(DateTime time)
            => time.Minute == 0 ? time.ToString("HH", CultureInfo.InvariantCulture) : time.ToString("HH:mm", CultureInfo.InvariantCulture);
    }
}
