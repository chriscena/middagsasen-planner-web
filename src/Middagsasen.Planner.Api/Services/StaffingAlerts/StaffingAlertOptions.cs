using Microsoft.Extensions.Options;

namespace Middagsasen.Planner.Api.Services.StaffingAlerts
{
    /// <summary>
    /// Innstillinger for bemanningsvarsel (SMS til admin når noen trekker seg fra en vakt, issue #43), bundet til
    /// seksjonen <c>StaffingAlerts</c>. Alle verdier har standardverdier, så ingen konfigurasjon er påkrevd.
    /// Verdiene valideres ved oppstart (<see cref="StaffingAlertOptionsValidator"/>).
    /// </summary>
    /// <remarks>
    /// Eksempel i <c>appsettings.json</c>:
    /// <code>
    /// "StaffingAlerts": { "NoticeDays": 2 }
    /// </code>
    /// </remarks>
    public class StaffingAlertOptions
    {
        public const string SectionName = "StaffingAlerts";

        /// <summary>
        /// Hvor mange kalenderdager fram i tid vakta høyst kan starte for at varselet sendes. 0 varsler bare for vakter
        /// i dag, 1 for i dag og i morgen, og 2 (standard) til og med i overmorgen.
        /// </summary>
        public int NoticeDays { get; set; } = 2;
    }

    /// <summary>
    /// Validerer <see cref="StaffingAlertOptions"/>: <c>NoticeDays</c> må være 0 eller større. Registreres med
    /// <c>ValidateOnStart</c>, så feil konfigurasjon stopper oppstarten.
    /// </summary>
    public sealed class StaffingAlertOptionsValidator : IValidateOptions<StaffingAlertOptions>
    {
        public ValidateOptionsResult Validate(string? name, StaffingAlertOptions options)
        {
            if (options.NoticeDays < 0)
                return ValidateOptionsResult.Fail($"{StaffingAlertOptions.SectionName}:{nameof(StaffingAlertOptions.NoticeDays)} må være 0 eller større (er {options.NoticeDays}).");

            return ValidateOptionsResult.Success;
        }
    }
}
