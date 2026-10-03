using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Core;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services.SmsSender;
using Middagsasen.Planner.Api.Services.Users;
using System.Security.Cryptography;
using System.Text;

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
                // Deaktiverte brukere mister tilgangen selv om sesjonen skulle finnes (Delete sletter den også).
                .Where(us => us.UserSessionId == id && !us.User.Inactive)
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
            user.FailedOtpAttempts = 0;
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

        /// <summary>
        /// Logger inn med engangskode eller passord. Begge prøves med samme <see cref="AuthRequest.Password"/>.
        /// </summary>
        /// <remarks>
        /// Mislykkes innloggingen mens brukeren har en gyldig engangskode, telles det som et feilforsøk mot koden,
        /// også når brukeren egentlig prøvde passordet (vi kan ikke skille dem). Etter
        /// <see cref="AuthOptions.MaxOtpAttempts"/> feilforsøk ugyldiggjøres koden, og brukeren må be om en ny.
        /// Feil passord uten gyldig engangskode påvirker ikke telleren. Svaret er <see cref="AuthStatus.AuthenticationFailed"/>
        /// i alle tilfeller, så det avsløres ikke at koden ble ugyldiggjort.
        /// Telling og bruk av koden skjer med atomiske oppdateringer i databasen: parallelle feilforsøk telles alle,
        /// og en riktig kode godtas bare hvis grensen ikke er nådd når koden brukes.
        /// </remarks>
        public async Task<AuthResponse> Authenticate(AuthRequest request)
        {
            var userName = request.UserName.ToNormalizedUserName();
            if (userName == null) return new AuthResponse { Status = AuthStatus.InvalidUsername };

            var user = await DbContext.Users.WhereUserName(userName).SingleOrDefaultAsync(user => !user.Inactive);
            if (user == null) return Failed();

            var activeOtp = HasActiveOtp(user) ? user.OneTimePassword : null;

            if (activeOtp != null && OtpEquals(activeOtp, request.Password) && await TryConsumeOtp(user.UserId, activeOtp))
                return await SignIn(user, AuthType.Otp);

            if (user.EncryptedPassword != null && user.Salt != null && PasswordHasher.VerifyHash(request.Password, user.Salt, user.EncryptedPassword))
            {
                user.OtpCreated = null;
                user.OneTimePassword = null;
                user.FailedOtpAttempts = 0;
                return await SignIn(user, AuthType.Password);
            }

            if (activeOtp != null)
                await RegisterFailedOtpAttempt(user.UserId, activeOtp);

            return Failed();
        }

        private static AuthResponse Failed() => new() { Status = AuthStatus.AuthenticationFailed };

        private bool HasActiveOtp(User user)
            => user.OneTimePassword != null
                && user.OtpCreated.HasValue
                && UtcNow < user.OtpCreated.Value.Add(_options.OtpLifetime)
                && user.FailedOtpAttempts < _options.MaxOtpAttempts;

        /// <summary>Sammenligner på konstant tid, så svartiden ikke avslører hvor mange tegn som stemmer.</summary>
        private static bool OtpEquals(string expected, string actual)
            => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual));

        /// <summary>
        /// Bruker opp koden: nullstiller den bare hvis den fortsatt er gjeldende kode og grensen for feilforsøk
        /// ikke er nådd. Gir <c>false</c> hvis en parallell forespørsel har brukt opp eller ugyldiggjort koden.
        /// </summary>
        private async Task<bool> TryConsumeOtp(int userId, string otp)
        {
            var max = _options.MaxOtpAttempts;
            var updated = await DbContext.Users
                .Where(u => u.UserId == userId && u.OneTimePassword == otp && u.FailedOtpAttempts < max)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(u => u.OneTimePassword, (string?)null)
                    .SetProperty(u => u.OtpCreated, (DateTime?)null)
                    .SetProperty(u => u.FailedOtpAttempts, 0));
            return updated == 1;
        }

        /// <summary>
        /// Øker telleren for gjeldende kode, og ugyldiggjør koden når grensen nås. Oppdateringen er atomisk
        /// (<c>FailedOtpAttempts = FailedOtpAttempts + 1</c>), så parallelle feilforsøk telles alle. Gjelder bare
        /// koden forsøket ble gjort mot; er det laget ny kode i mellomtiden, røres den ikke.
        /// </summary>
        private async Task RegisterFailedOtpAttempt(int userId, string otp)
        {
            var max = _options.MaxOtpAttempts;
            await DbContext.Users
                .Where(u => u.UserId == userId && u.OneTimePassword == otp)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(u => u.FailedOtpAttempts, u => u.FailedOtpAttempts + 1)
                    .SetProperty(u => u.OneTimePassword, u => u.FailedOtpAttempts + 1 >= max ? null : u.OneTimePassword)
                    .SetProperty(u => u.OtpCreated, u => u.FailedOtpAttempts + 1 >= max ? null : u.OtpCreated));
        }

        /// <summary>Oppretter sesjon og token. Lagrer også endringer på brukeren (f.eks. nullstilt engangskode).</summary>
        private async Task<AuthResponse> SignIn(User user, AuthType authType)
        {
            var session = await CreateSession(user, authType);
            var token = _sessionTokens.Create(session.UserSessionId);
            return new AuthResponse { Status = AuthStatus.Success, Token = token };
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
                Created = UtcNow,
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
