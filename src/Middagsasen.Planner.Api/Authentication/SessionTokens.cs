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

        private readonly AuthOptions _options;
        private readonly TimeProvider _timeProvider;
        private readonly SymmetricSecurityKey? _key;
        private readonly JwtSecurityTokenHandler _handler = new() { MapInboundClaims = false };

        public SessionTokens(IAuthSettings authSettings, IOptions<AuthOptions> options, TimeProvider timeProvider)
        {
            _options = options.Value;
            _timeProvider = timeProvider;
            // Nøkkelen bygges bare når hemmeligheten er satt, slik at appen kan starte uten (f.eks. ved
            // build-time-generering av OpenAPI). Bruk uten hemmelighet gir en tydelig feil, se Key.
            _key = string.IsNullOrEmpty(authSettings.Secret)
                ? null
                : new SymmetricSecurityKey(Encoding.ASCII.GetBytes(authSettings.Secret));
        }

        private SymmetricSecurityKey Key => _key
            ?? throw new InvalidOperationException("Infrastructure:Secret er ikke satt. Kan ikke utstede eller validere tokens.");

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
                SigningCredentials = new SigningCredentials(Key, Algorithm),
            };
            return _handler.WriteToken(_handler.CreateToken(descriptor));
        }

        public Guid? ReadSessionId(string token)
        {
            var parameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = Key,
                ValidAlgorithms = new[] { Algorithm },
                ValidateIssuer = true,
                ValidIssuer = _options.Issuer,
                ValidateAudience = true,
                ValidAudience = _options.Audience,
                ValidateLifetime = true,
                RequireExpirationTime = true,
                ClockSkew = TimeSpan.Zero,
                // Standardvalideringen av levetid bruker systemklokka. Vi validerer mot injisert TimeProvider,
                // slik at utstedelse og validering bruker samme klokke (og levetid kan testes).
                LifetimeValidator = IsWithinLifetime,
            };

            ClaimsPrincipal principal;
            try
            {
                principal = _handler.ValidateToken(token, parameters, out _);
            }
            catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
            {
                // Ugyldig, utløpt eller feilformatert token. Feilformaterte tokens gir ArgumentException.
                return null;
            }

            var sessionId = principal.FindFirst(SessionIdClaim)?.Value;
            return Guid.TryParse(sessionId, out var id) ? id : null;
        }

        private bool IsWithinLifetime(DateTime? notBefore, DateTime? expires, SecurityToken token, TokenValidationParameters parameters)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            if (expires is not { } validTo || now >= validTo) return false;
            return notBefore is not { } validFrom || now >= validFrom;
        }
    }
}
