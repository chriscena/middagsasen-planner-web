using Microsoft.Extensions.Options;

namespace Middagsasen.Planner.Api.Authentication
{
    /// <summary>
    /// Innstillinger for innlogging, bundet til seksjonen <c>Auth</c>. Alle verdier har standardverdier,
    /// så ingen konfigurasjon er påkrevd. Hemmeligheten som signerer tokenet ligger i <c>Infrastructure:Secret</c>
    /// (<see cref="IAuthSettings"/>). Verdiene valideres ved oppstart (<see cref="AuthOptionsValidator"/>).
    /// </summary>
    /// <remarks>
    /// Tidsrom angis som <c>TimeSpan</c>-strenger, f.eks. <c>"00:30:00"</c> for 30 minutter.
    /// Et rent tall som <c>"30"</c> tolkes som 30 dager.
    /// </remarks>
    public class AuthOptions
    {
        public const string SectionName = "Auth";

        /// <summary>Utsteder (<c>iss</c>) i tokenet. Tokens med annen utsteder avvises.</summary>
        public string Issuer { get; set; } = "middagsasen-planner";

        /// <summary>Mottaker (<c>aud</c>) i tokenet. Tokens med annen mottaker avvises.</summary>
        public string Audience { get; set; } = "middagsasen-planner-web";

        /// <summary>Hvor lenge et token er gyldig etter innlogging.</summary>
        public TimeSpan TokenLifetime { get; set; } = TimeSpan.FromDays(7);

        /// <summary>Hvor lenge en bruker må vente før en ny engangskode kan sendes.</summary>
        public TimeSpan OtpThrottle { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>Hvor lenge en engangskode er gyldig.</summary>
        public TimeSpan OtpLifetime { get; set; } = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Antall feilforsøk mot en engangskode før den ugyldiggjøres. Brukeren må da be om ny kode.
        /// </summary>
        public int MaxOtpAttempts { get; set; } = 5;
    }

    /// <summary>
    /// Validerer <see cref="AuthOptions"/>: alle tidsrom må være positive, <c>MaxOtpAttempts</c> minst 1,
    /// og utsteder og mottaker kan ikke være tomme. Registreres med <c>ValidateOnStart</c>, så feil konfigurasjon
    /// stopper oppstarten i stedet for å gi feil per kall.
    /// </summary>
    public sealed class AuthOptionsValidator : IValidateOptions<AuthOptions>
    {
        public ValidateOptionsResult Validate(string? name, AuthOptions options)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(options.Issuer))
                errors.Add($"{AuthOptions.SectionName}:{nameof(AuthOptions.Issuer)} kan ikke være tom.");
            if (string.IsNullOrWhiteSpace(options.Audience))
                errors.Add($"{AuthOptions.SectionName}:{nameof(AuthOptions.Audience)} kan ikke være tom.");

            AddIfNotPositive(errors, nameof(AuthOptions.TokenLifetime), options.TokenLifetime);
            AddIfNotPositive(errors, nameof(AuthOptions.OtpThrottle), options.OtpThrottle);
            AddIfNotPositive(errors, nameof(AuthOptions.OtpLifetime), options.OtpLifetime);

            if (options.MaxOtpAttempts < 1)
                errors.Add($"{AuthOptions.SectionName}:{nameof(AuthOptions.MaxOtpAttempts)} må være minst 1 (er {options.MaxOtpAttempts}).");

            return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
        }

        private static void AddIfNotPositive(List<string> errors, string property, TimeSpan value)
        {
            if (value <= TimeSpan.Zero)
                errors.Add($"{AuthOptions.SectionName}:{property} må være større enn 0 (er {value}). Angi tidsrom som \"hh:mm:ss\", f.eks. \"00:30:00\".");
        }
    }
}
