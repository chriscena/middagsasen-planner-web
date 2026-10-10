using Microsoft.Extensions.Options;

namespace Middagsasen.Planner.Api.Services.Reminders
{
    /// <summary>
    /// Innstillinger for vaktpåminnelse (SMS dagen før vakt), bundet til seksjonen <c>Reminders</c>. Alle verdier har
    /// standardverdier, så ingen konfigurasjon er påkrevd. Verdiene valideres ved oppstart (<see cref="ReminderOptionsValidator"/>).
    /// </summary>
    /// <remarks>
    /// Klokkeslett og tidsrom angis som <c>TimeSpan</c>-strenger, f.eks. <c>"17:00"</c> for kl. 17 og <c>"00:05:00"</c>
    /// for 5 minutter. Et rent tall som <c>"5"</c> tolkes som 5 dager, og avvises av valideringen for klokkeslettene.
    /// </remarks>
    public class ReminderOptions
    {
        public const string SectionName = "Reminders";

        /// <summary>Om påminnelser sendes i det hele tatt. Av gjør at bakgrunnsjobben avslutter ved oppstart.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Klokkeslettet (norsk lokal tid) påminnelsene for neste dag sendes fra.</summary>
        public TimeSpan SendTime { get; set; } = new(17, 0, 0);

        /// <summary>
        /// Klokkeslettet (norsk lokal tid) det gis opp. En sending som feiler, prøves på nytt ved hver kjøring fram til da.
        /// Må være etter <see cref="SendTime"/>.
        /// </summary>
        public TimeSpan RetryUntil { get; set; } = new(22, 0, 0);

        /// <summary>Hvor ofte bakgrunnsjobben sjekker om det er noe å sende.</summary>
        public TimeSpan PollInterval { get; set; } = TimeSpan.FromMinutes(5);
    }

    /// <summary>
    /// Validerer <see cref="ReminderOptions"/>: <c>SendTime</c> og <c>RetryUntil</c> må være klokkeslett (fra 00:00 til
    /// og med 23:59:59), <c>SendTime</c> før <c>RetryUntil</c>, og <c>PollInterval</c> positiv. Registreres med
    /// <c>ValidateOnStart</c>, så feil konfigurasjon stopper oppstarten.
    /// </summary>
    public sealed class ReminderOptionsValidator : IValidateOptions<ReminderOptions>
    {
        public ValidateOptionsResult Validate(string? name, ReminderOptions options)
        {
            var errors = new List<string>();

            AddIfNotTimeOfDay(errors, nameof(ReminderOptions.SendTime), options.SendTime);
            AddIfNotTimeOfDay(errors, nameof(ReminderOptions.RetryUntil), options.RetryUntil);

            if (options.SendTime >= options.RetryUntil)
                errors.Add($"{ReminderOptions.SectionName}:{nameof(ReminderOptions.SendTime)} ({options.SendTime}) må være før {nameof(ReminderOptions.RetryUntil)} ({options.RetryUntil}).");

            if (options.PollInterval <= TimeSpan.Zero)
                errors.Add($"{ReminderOptions.SectionName}:{nameof(ReminderOptions.PollInterval)} må være større enn 0 (er {options.PollInterval}). Angi tidsrom som \"hh:mm:ss\", f.eks. \"00:05:00\".");

            return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
        }

        private static void AddIfNotTimeOfDay(List<string> errors, string property, TimeSpan value)
        {
            if (value < TimeSpan.Zero || value >= TimeSpan.FromDays(1))
                errors.Add($"{ReminderOptions.SectionName}:{property} må være et klokkeslett mellom 00:00 og 23:59:59 (er {value}). Angi som \"hh:mm\", f.eks. \"17:00\".");
        }
    }
}
