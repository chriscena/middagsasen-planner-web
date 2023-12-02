using Microsoft.IdentityModel.Tokens;
using Middagsasen.Planner.Api.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Middagsasen.Planner.Api.Authentication
{
    public interface IAuthSettings
    {
        string Secret { get; }
    }

    public class JwtMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IAuthSettings _authSettings;

        public JwtMiddleware(RequestDelegate next, IAuthSettings appSettings)
        {
            _next = next;
            _authSettings = appSettings;
        }

        public async Task Invoke(HttpContext context, IAuthenticationService userService)
        {
            var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();

            if (token != null)
                await AttachUserToContext(context, userService, token);
            await _next(context);
        }

        private async Task AttachUserToContext(HttpContext context, IAuthenticationService userService, string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.ASCII.GetBytes(_authSettings.Secret);
                tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ClockSkew = TimeSpan.Zero
                }, out SecurityToken validatedToken);

                var jwtToken = (JwtSecurityToken)validatedToken;
                var sessionIdString = jwtToken.Claims.First(x => x.Type == "id").Value;
                var sessionId = Guid.Parse(sessionIdString);

                var user = await userService.GetUserBySessionId(sessionId);
                if (user == null) return;

                context.Items["User"] = user;
                context.User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[]
                    {
                        new Claim(ClaimTypes.Sid, user.Id.ToString(), ClaimValueTypes.Integer),
                        new Claim(ClaimTypes.MobilePhone, user.UserName, ClaimValueTypes.String),
                        new Claim(ClaimTypes.Name, user.FullName ?? "", ClaimValueTypes.String),
                        new Claim(ClaimTypes.GivenName, user.FirstName ?? "", ClaimValueTypes.String),
                        new Claim(ClaimTypes.Surname, user.LastName ?? "", ClaimValueTypes.String),
                        new Claim(ClaimTypes.Role, user.IsAdmin ? "Administrator" : "User", ClaimValueTypes.String),
                        new Claim(ClaimTypes.Authentication, sessionIdString, ClaimValueTypes.String)
                    },
                    "Password", ClaimTypes.Name, ClaimTypes.Role)); ;
            }
            catch
            {

            }
        }
    }
}
