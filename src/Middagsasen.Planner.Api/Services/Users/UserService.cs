using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Core;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services.SmsSender;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Middagsasen.Planner.Api.Services.Users
{
    public class UserService : IUserService
    {
        public UserService(PlannerDbContext dbContext, ISmsSender smsSender, IAuthSettings authSettings)
        {
            DbContext = dbContext;
            SmsSender = smsSender;
            AuthSettings = authSettings;
        }

        public PlannerDbContext DbContext { get; }
        public ISmsSender SmsSender { get; }
        public IAuthSettings AuthSettings { get; }

        public async Task<IEnumerable<UserResponse>> GetUsers()
        {
            var users =  await DbContext.Users.AsNoTracking().ToListAsync();
            return users.Select(Map).ToList();
        }

        public async Task<UserResponse?> GetUserById(int id)
        {
            var user = await DbContext.Users.AsNoTracking().SingleOrDefaultAsync(u => u.UserId == id);
            return user != null ? Map(user) : null;
        }

        public async Task<UserResponse?> GetUserBySessionId(Guid id)
        {
            var session = await DbContext.UserSessions.AsNoTracking().Include(us => us.User).SingleOrDefaultAsync(u => u.UserSessionId == id);
            return session?.User != null ? Map(session.User) : null;
        }

        public async Task<OtpResponse> GenerateOtpForUser(OtpRequest request)
        {
            var phoneNumber = request.UserName.ToNumericPhoneNo();
            if (phoneNumber == 0) return new OtpResponse { Status = OtpStatus.InvalidPhoneNumber };

            var userName = phoneNumber.ToUserName();

            var user = await DbContext.Users.SingleOrDefaultAsync(u => u.UserName == userName);

            if (user != null && user.OtpCreated.HasValue && DateTime.UtcNow < user.OtpCreated.Value.AddMinutes(5))
            {
                return new OtpResponse
                {
                    Status = OtpStatus.TooManyRequests,
                };
            }

            user ??= DbContext.Users.Add(new User { UserName = userName, Created = DateTime.UtcNow }).Entity;

            user.OneTimePassword = CreateOneTimePassword();
            user.OtpCreated = DateTime.UtcNow;
            await DbContext.SaveChangesAsync();

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
            var phoneNumber = request.UserName.ToNumericPhoneNo();
            if (phoneNumber == 0) return new AuthResponse { Status = AuthStatus.InvalidUsername };

            var userName = phoneNumber.ToUserName();

            var user = await DbContext.Users.SingleOrDefaultAsync(user => user.UserName == userName);

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
                    return  new AuthResponse { Status = AuthStatus.Success, Token = token };
                }
            }

            return new AuthResponse { Status = AuthStatus.AuthenticationFailed };
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
        private string CreateOneTimePassword()
        {
            return _random.Next(0, 9999).ToString("D4");
        }

        private UserResponse Map(User user)
        {
            return new UserResponse
            {
                Id = user.UserId,
                UserName = user.UserName,
                FirstName = user.FirstName,
                LastName = user.LastName,
                FullName = $"{user.FirstName ?? ""} {user.LastName ?? ""}".Trim(),
                IsAdmin = user.IsAdmin,
            };
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

    public static class UserNameExtensions 
    {
        public static string ToUserName(this long phoneNumber)
        {
            return phoneNumber.ToString().Substring(2);
        }
    }

    public enum AuthType
    {
        Otp,
        Password,
    }
}
