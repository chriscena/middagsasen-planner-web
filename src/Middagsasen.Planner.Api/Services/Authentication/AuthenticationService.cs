using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Core;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services.SmsSender;
using Middagsasen.Planner.Api.Services.Users;
using System.Security.Cryptography;

namespace Middagsasen.Planner.Api.Services.Authentication
{
    public class AuthenticationService : IAuthenticationService
    {
        private readonly ISessionTokens _sessionTokens;
        private readonly AuthOptions _options;
        private readonly TimeProvider _timeProvider;

        public AuthenticationService(
            PlannerDbContext dbContext,
            ISmsSender smsSender,
            ISessionTokens sessionTokens,
            IOptions<AuthOptions> options,
            TimeProvider timeProvider)
        {
            DbContext = dbContext;
            SmsSender = smsSender;
            _sessionTokens = sessionTokens;
            _options = options.Value;
            _timeProvider = timeProvider;
        }

        private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

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
                var newUser = DbContext.Users.Add(new User { UserName = userName, Created = UtcNow }).Entity;
                SetOneTimePassword(newUser);
                if (await DbContext.TrySaveWithUniqueUserName(userName, newUser.UserId))
                    return await SendOneTimePassword(newUser);

                // Brukeren ble opprettet samtidig av en annen forespørsel. Fortsett med den.
                DbContext.Entry(newUser).State = EntityState.Detached;
                user = await DbContext.Users.WhereUserName(userName).SingleAsync();
            }

            if (user.OtpCreated.HasValue && UtcNow < user.OtpCreated.Value.Add(_options.OtpThrottle))
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
            user.OtpCreated = UtcNow;
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
                if (user.OtpCreated.HasValue && user.OneTimePassword == request.Password && UtcNow < user.OtpCreated.Value.Add(_options.OtpLifetime))
                {
                    var session = await CreateSession(user, AuthType.Otp);

                    var token = _sessionTokens.Create(session.UserSessionId);

                    user.OtpCreated = null;
                    user.OneTimePassword = null;
                    await DbContext.SaveChangesAsync();
                    return new AuthResponse { Status = AuthStatus.Success, Token = token };
                }
                if (user.EncryptedPassword != null && user.Salt != null && PasswordHasher.VerifyHash(request.Password, user.Salt, user.EncryptedPassword))
                {
                    var session = await CreateSession(user, AuthType.Password);

                    var token = _sessionTokens.Create(session.UserSessionId);

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

        public PlannerDbContext DbContext { get; }
        public ISmsSender SmsSender { get; }

        /// <summary>Firesifret engangskode (0000–9999) fra en kryptografisk sikker tilfeldighetskilde.</summary>
        private static string CreateOneTimePassword()
        {
            return RandomNumberGenerator.GetInt32(0, 10000).ToString("D4");
        }
    }
}
