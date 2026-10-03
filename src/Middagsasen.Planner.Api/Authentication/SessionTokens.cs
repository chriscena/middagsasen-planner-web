using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Middagsasen.Planner.Api.Authentication
{
    /// <summary>
    /// Utsteder og leser innloggingstokens (JWT). Tokenet bærer bare id-en til brukerens sesjon (claim <c>id</c>);
    /// alt annet om brukeren slås opp fra sesjonen.
    /// </summary>
    public interface ISessionTokens
    {
        /// <summary>Lager et signert token for sesjonen, gyldig i <see cref="AuthOptions.TokenLifetime"/>.</summary>
        string Create(Guid sessionId);

        /// <summary>
        /// Validerer tokenet (signatur, utsteder, mottaker og levetid) og returnerer sesjons-id-en,
        /// eller <c>null</c> hvis tokenet er ugyldig, utløpt eller feilformatert. Kaster ikke for ugyldige tokens.
        /// </summary>
        Guid? ReadSessionId(string token);
    }

    /// <summary>
    /// Eier all kunnskap om tokenformatet: nøkkel, algoritme, utsteder, mottaker og levetid.
    /// Tid hentes fra <see cref="TimeProvider"/>, både ved utstedelse og validering.
    /// </summary>
    public sealed class SessionTokens : ISessionTokens
    {
        private const string SessionIdClaim = "id";
        private const string Algorithm = SecurityAlgorithms.HmacSha256;

        /// <summary>HS256 krever en nøkkel på minst 256 bit.</summary>
        public const int MinSecretBytes = 32;

        public const string InvalidSecretMessage =
            "Infrastructure:Secret må være satt og være minst 32 tegn (HS256 krever en nøkkel på minst 256 bit).";

        /// <summary>
        /// Om hemmeligheten kan brukes som nøkkel. Nøkkelen er ASCII-bytene til hemmeligheten, så antall bytes er antall tegn.
        /// Valideres ved oppstart (se Program.cs), slik at appen ikke starter med manglende eller for kort hemmelighet.
        /// </summary>
        public static bool IsValidSecret(string? secret)
            => !string.IsNullOrEmpty(secret) && Encoding.ASCII.GetByteCount(secret) >= MinSecretBytes;

        private readonly AuthOptions _options;
        private readonly TimeProvider _timeProvider;
        private readonly SymmetricSecurityKey _key;
        private readonly TokenValidationParameters _validationParameters;
        private readonly JwtSecurityTokenHandler _handler = new() { MapInboundClaims = false };

        public SessionTokens(IAuthSettings authSettings, IOptions<AuthOptions> options, TimeProvider timeProvider)
        {
            // Hemmeligheten valideres ved oppstart, så dette slår bare til hvis klassen brukes uten den valideringen.
            if (!IsValidSecret(authSettings.Secret))
                throw new InvalidOperationException(InvalidSecretMessage);

            _options = options.Value;
            _timeProvider = timeProvider;
            _key = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(authSettings.Secret));
            _validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = _key,
                ValidAlgorithms = new[] { Algorithm },
                ValidateIssuer = true,
                ValidIssuer = _options.Issuer,
                ValidateAudience = true,
                ValidAudience = _options.Audience,
                // Standardvalideringen av levetid bruker systemklokka. Vi validerer mot injisert TimeProvider,
                // slik at utstedelse og validering bruker samme klokke (og levetid kan testes).
                // Med egen LifetimeValidator ignorerer IdentityModel ValidateLifetime, RequireExpirationTime og
                // ClockSkew, så de settes ikke her: krav om utløpstid og null slingringsmonn ligger i IsWithinLifetime.
                LifetimeValidator = IsWithinLifetime,
            };
        }

        public string Create(Guid sessionId)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var descriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[] { new Claim(SessionIdClaim, sessionId.ToString()) }),
                Issuer = _options.Issuer,
                Audience = _options.Audience,
                IssuedAt = now,
                NotBefore = now,
                Expires = now.Add(_options.TokenLifetime),
                SigningCredentials = new SigningCredentials(_key, Algorithm),
            };
            return _handler.WriteToken(_handler.CreateToken(descriptor));
        }

        public Guid? ReadSessionId(string token)
        {
            ClaimsPrincipal principal;
            try
            {
                principal = _handler.ValidateToken(token, _validationParameters, out _);
            }
            catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
            {
                // Ugyldig eller utløpt token gir SecurityTokenException. Feilformaterte tokens gir ArgumentException
                // (tomt token: ArgumentNullException, ikke-JWT: SecurityTokenMalformedException, ugyldig base64: ArgumentException).
                return null;
            }

            var sessionId = principal.FindFirst(SessionIdClaim)?.Value;
            return Guid.TryParse(sessionId, out var id) ? id : null;
        }

        /// <summary>Krever utløpstid. Ingen slingringsmonn: tokenet er gyldig fra <c>nbf</c> til (men ikke med) <c>exp</c>.</summary>
        private bool IsWithinLifetime(DateTime? notBefore, DateTime? expires, SecurityToken token, TokenValidationParameters parameters)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            if (expires is not { } validTo || now >= validTo) return false;
            return notBefore is not { } validFrom || now >= validFrom;
        }
    }
}
