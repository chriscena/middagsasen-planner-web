using Microsoft.EntityFrameworkCore;
using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services.SmsSender;

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
