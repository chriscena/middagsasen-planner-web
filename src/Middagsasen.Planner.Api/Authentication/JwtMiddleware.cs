using Middagsasen.Planner.Api.Services.Authentication;
using System.Net.Http.Headers;
using System.Security.Claims;

namespace Middagsasen.Planner.Api.Authentication
{
    public interface IAuthSettings
    {
        string Secret { get; }
    }

    /// <summary>
    /// Leser <c>Authorization: Bearer &lt;token&gt;</c> og legger innlogget bruker i <c>HttpContext.Items["User"]</c>.
    /// Mangler eller ugyldig token gir en anonym forespørsel (<see cref="AuthorizeAttribute"/> gir da 401).
    /// Andre feil, f.eks. databasefeil ved oppslag av sesjonen, bobler videre til <see cref="ExceptionHandlingMiddleware"/>.
    /// </summary>
    public class JwtMiddleware
    {
        private const string BearerScheme = "Bearer";

        private readonly RequestDelegate _next;
        private readonly ILogger<JwtMiddleware> _logger;

        public JwtMiddleware(RequestDelegate next, ILogger<JwtMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        /// <remarks>
        /// <see cref="ISessionTokens"/> hentes per forespørsel i stedet for i konstruktøren. Middleware-konstruktøren
        /// kjører når pipelinen bygges, også under build-time-genereringen av OpenAPI, der hemmeligheten mangler
        /// og <see cref="SessionTokens"/> ikke kan lages.
        /// </remarks>
        public async Task Invoke(HttpContext context, ISessionTokens sessionTokens, IAuthenticationService userService)
        {
            var token = ReadBearerToken(context.Request);
            if (token != null)
                await AttachUserToContext(context, sessionTokens, userService, token);
            await _next(context);
        }

        /// <summary>Henter tokenet fra headeren hvis den er på formen <c>Bearer &lt;token&gt;</c>, ellers <c>null</c>.</summary>
        private static string? ReadBearerToken(HttpRequest request)
        {
            var header = request.Headers.Authorization.FirstOrDefault();
            if (!AuthenticationHeaderValue.TryParse(header, out var value)) return null;
            if (!string.Equals(value.Scheme, BearerScheme, StringComparison.OrdinalIgnoreCase)) return null;
            return string.IsNullOrWhiteSpace(value.Parameter) ? null : value.Parameter;
        }

        private async Task AttachUserToContext(HttpContext context, ISessionTokens sessionTokens, IAuthenticationService userService, string token)
        {
            if (sessionTokens.ReadSessionId(token) is not { } sessionId)
            {
                // Logg aldri selve tokenet.
                _logger.LogInformation("Ugyldig eller utløpt token. Forespørselen behandles som anonym.");
                return;
            }

            if (await userService.GetUserBySessionId(sessionId) is not { } user)
            {
                _logger.LogInformation("Fant ingen sesjon for tokenet (logget ut eller slettet). Forespørselen behandles som anonym.");
                return;
            }

            context.Items["User"] = user;
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.Sid, user.UserId.ToString(), ClaimValueTypes.Integer),
                    new Claim(ClaimTypes.Role, user.IsAdmin ? Roles.Administrator : Roles.User, ClaimValueTypes.String),
                    new Claim(ClaimTypes.Authentication, sessionId.ToString(), ClaimValueTypes.String)
                },
                "Password", ClaimTypes.Name, ClaimTypes.Role));
        }
    }
}
