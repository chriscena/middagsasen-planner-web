namespace Middagsasen.Planner.Api.Authentication
{
    /// <summary>
    /// Innstillinger for innlogging, bundet til seksjonen <c>Auth</c>. Alle verdier har standardverdier,
    /// så ingen konfigurasjon er påkrevd. Hemmeligheten som signerer tokenet ligger i <c>Infrastructure:Secret</c>
    /// (<see cref="IAuthSettings"/>).
    /// </summary>
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
    }
}
