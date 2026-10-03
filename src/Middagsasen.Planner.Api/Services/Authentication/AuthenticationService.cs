using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Core;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services.SmsSender;
using Middagsasen.Planner.Api.Services.Users;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Middagsasen.Planner.Api.Services.Authentication
{
    public class AuthenticationService : IAuthenticationService
    {
        public AuthenticationService(PlannerDbContext dbContext, ISmsSender smsSender, IAuthSettings authSettings)
        {
            DbContext = dbContext;
            SmsSender = smsSender;
            AuthSettings = authSettings;
        }
        public async Task<Actor?> GetUserBySessionId(Guid id)
        {
            var user = await DbContext.UserSessions
                .AsNoTracking()
                .Where(us => us.UserSessionId == id)
                .Select(us => new { us.User.UserId, us.User.IsAdmin })
                .SingleOrDefaultAsync();
            return user != null ? new Actor(user.UserId, user.IsAdmin) : null;
        }

        /// <summary>
        /// Sender engangskode på SMS. Finnes ingen bruker med nummeret (normalisert), opprettes en ny.
        /// </summary>
        /// <remarks>
        /// Bare norske numre godtas (se <see cref="UserNameExtensions.ToNormalizedUserName"/>), og SMS-en sendes til
        /// nummeret som utledes av det normaliserte brukernavnet, så koden går alltid til eieren av brukeren.
        /// Oppslaget filtrerer ikke på <c>Inactive</c>, så en inaktiv bruker gir aldri en ny rad.
        /// Oppretter en parallell forespørsel (OTP eller administrator) samme bruker mellom oppslaget og lagringen,
        /// avviser den unike indeksen på <c>Users.UserName</c> vår rad. Da fortsetter vi med den eksisterende brukeren,
        /// med samme sjekk mot for mange forespørsler som ellers.
        /// </remarks>
        public async Task<OtpResponse> GenerateOtpForUser(OtpRequest request)
        {
            var userName = request.UserName.ToNormalizedUserName();
            if (userName == null) return new OtpResponse { Status = OtpStatus.InvalidPhoneNumber };

            var user = await DbContext.Users.WhereUserName(userName).SingleOrDefaultAsync();

            if (user == null)
            {
                var newUser = DbContext.Users.Add(new User { UserName = userName, Created = DateTime.UtcNow }).Entity;
                SetOneTimePassword(newUser);
                if (await DbContext.TrySaveWithUniqueUserName(userName, newUser.UserId))
                    return await SendOneTimePassword(newUser);

                // Brukeren ble opprettet samtidig av en annen forespørsel. Fortsett med den.
                DbContext.Entry(newUser).State = EntityState.Detached;
                user = await DbContext.Users.WhereUserName(userName).SingleAsync();
            }

            if (user.OtpCreated.HasValue && DateTime.UtcNow < user.OtpCreated.Value.AddMinutes(5))
            {
                return new OtpResponse
                {
                    Status = OtpStatus.TooManyRequests,
                };
            }

            SetOneTimePassword(user);
            await DbContext.SaveChangesAsync();

            return await SendOneTimePassword(user);
        }

        private void SetOneTimePassword(User user)
        {
            user.OneTimePassword = CreateOneTimePassword();
            user.OtpCreated = DateTime.UtcNow;
        }

        /// <summary>
        /// Sender koden til nummeret som utledes av brukerens (normaliserte) brukernavn, ikke til det som ble skrevet inn.
        /// </summary>
        private async Task<OtpResponse> SendOneTimePassword(User user)
        {
            var sms = new SmsMessage
            {
                ReceiverPhoneNo = user.UserName.ToSmsPhoneNo(),
                Body = $"Din engangskode er {user.OneTimePassword}.",
                SmsNotificationId = Guid.NewGuid(),
            };

            await SmsSender.SendMessages(new[] { sms });

            return new OtpResponse { Status = OtpStatus.Sent };
        }

        public async Task<AuthResponse> Authenticate(AuthRequest request)
        {
            var userName = request.UserName.ToNormalizedUserName();
            if (userName == null) return new AuthResponse { Status = AuthStatus.InvalidUsername };

            var user = await DbContext.Users.WhereUserName(userName).SingleOrDefaultAsync(user => !user.Inactive);

            if (user != null)
            {
                var now = DateTime.UtcNow;
                if (user.OtpCreated.HasValue && user.OneTimePassword == request.Password && now < user.OtpCreated.Value.AddMinutes(30))
                {
                    var session = await CreateSession(user, AuthType.Otp);

                    var token = GenerateJwtToken(session);

                    user.OtpCreated = null;
                    user.OneTimePassword = null;
                    await DbContext.SaveChangesAsync();
                    return new AuthResponse { Status = AuthStatus.Success, Token = token };
                }
                if (user.EncryptedPassword != null && user.Salt != null && PasswordHasher.VerifyHash(request.Password, user.Salt, user.EncryptedPassword))
                {
                    var session = await CreateSession(user, AuthType.Password);

                    var token = GenerateJwtToken(session);

                    user.OtpCreated = null;
                    user.OneTimePassword = null;
                    await DbContext.SaveChangesAsync();
                    return new AuthResponse { Status = AuthStatus.Success, Token = token };
                }
            }

            return new AuthResponse { Status = AuthStatus.AuthenticationFailed };
        }

        public async Task LogOut(Guid sessionId)
        {
            var session = await DbContext.UserSessions.SingleOrDefaultAsync(u => u.UserSessionId == sessionId);
            if (session == null) return;

            DbContext.UserSessions.Remove(session);
            await DbContext.SaveChangesAsync();
        }

        private async Task<UserSession> CreateSession(User user, AuthType authType)
        {
            var session = new UserSession
            {
                UserId = user.UserId,
                AuthType = authType,
            };
            DbContext.UserSessions.Add(session);
            await DbContext.SaveChangesAsync();

            return session;
        }

        private Random _random = new Random();

        public PlannerDbContext DbContext { get; }
        public ISmsSender SmsSender { get; }
        public IAuthSettings AuthSettings { get; }

        private string CreateOneTimePassword()
        {
            return _random.Next(0, 9999).ToString("D4");
        }

        private string GenerateJwtToken(UserSession session)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(AuthSettings.Secret);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[] { new Claim("id", session.UserSessionId.ToString()) }),
                Expires = DateTime.UtcNow.AddDays(7),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}
