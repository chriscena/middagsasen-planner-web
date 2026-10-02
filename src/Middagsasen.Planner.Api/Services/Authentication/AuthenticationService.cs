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
        public async Task<UserResponse?> GetUserBySessionId(Guid id)
        {
            var session = await DbContext.UserSessions.AsNoTracking().Include(us => us.User).SingleOrDefaultAsync(u => u.UserSessionId == id);
            return session?.User != null ? Map(session.User) : null;
        }

        /// <summary>
        /// Sender engangskode på SMS. Finnes ingen bruker med nummeret (normalisert), opprettes en ny.
        /// </summary>
        /// <remarks>
        /// Oppslaget filtrerer ikke på <c>Inactive</c>, så en inaktiv bruker gir aldri en ny rad.
        /// Oppretter en parallell forespørsel samme bruker samtidig, avviser den unike indeksen på
        /// <c>Users.UserName</c> den ene. Da har den andre nettopp sendt en kode, og vi svarer
        /// <see cref="OtpStatus.TooManyRequests"/>.
        /// </remarks>
        public async Task<OtpResponse> GenerateOtpForUser(OtpRequest request)
        {
            var userName = request.UserName.ToNormalizedUserName();
            if (userName == null) return new OtpResponse { Status = OtpStatus.InvalidPhoneNumber };

            // SMS-mottakeren trenger nummeret med landskode.
            var phoneNumber = request.UserName.ToNumericPhoneNo();

            var user = await DbContext.Users.WhereUserName(userName).SingleOrDefaultAsync();

            if (user != null && user.OtpCreated.HasValue && DateTime.UtcNow < user.OtpCreated.Value.AddMinutes(5))
            {
                return new OtpResponse
                {
                    Status = OtpStatus.TooManyRequests,
                };
            }

            var isNewUser = user == null;
            user ??= DbContext.Users.Add(new User { UserName = userName, Created = DateTime.UtcNow }).Entity;

            user.OneTimePassword = CreateOneTimePassword();
            user.OtpCreated = DateTime.UtcNow;
            try
            {
                await DbContext.SaveChangesAsync();
            }
            catch (DbUpdateException) when (isNewUser)
            {
                // Sjekk på nytt i stedet for å tolke leverandørspesifikke feilkoder.
                DbContext.Entry(user).State = EntityState.Detached;
                if (await DbContext.Users.WhereUserName(userName).AnyAsync())
                    return new OtpResponse { Status = OtpStatus.TooManyRequests };
                throw;
            }

            var sms = new SmsMessage
            {
                ReceiverPhoneNo = phoneNumber,
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

        private UserResponse Map(User user)
        {
            return new UserResponse
            {
                Id = user.UserId,
                PhoneNo = user.UserName,
                FirstName = user.FirstName,
                LastName = user.LastName,
                FullName = $"{user.FirstName ?? ""} {user.LastName ?? ""}".Trim(),
                IsAdmin = user.IsAdmin,
            };
        }
    }
}
