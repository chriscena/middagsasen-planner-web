namespace Middagsasen.Planner.Api.Authentication
{
    using Microsoft.IdentityModel.Tokens;
    using Middagsasen.Planner.Api.Services;
    using Middagsasen.Planner.Api.Services.Users;
    using System.IdentityModel.Tokens.Jwt;
    using System.Security.Claims;
    using System.Text;

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

        public async Task Invoke(HttpContext context, IUserService userService)
        {
            var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();

            if (token != null)
                await AttachUserToContext(context, userService, token);
            await _next(context);
        }

        private async Task AttachUserToContext(HttpContext context, IUserService userService, string token)
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
                    // set clockskew to zero so tokens expire exactly at token expiration time (instead of 5 minutes later)
                    ClockSkew = TimeSpan.Zero
                }, out SecurityToken validatedToken);

                var jwtToken = (JwtSecurityToken)validatedToken;
                var sessionId = Guid.Parse(jwtToken.Claims.First(x => x.Type == "id").Value);

                // attach user to context on successful jwt validation
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
                    },
                    "Password", ClaimTypes.Name, ClaimTypes.Role));
            }
            catch
            {
                // do nothing if jwt validation fails
                // user is not attached to context so request won't have access to secure routes
            }
        }
    }
}
