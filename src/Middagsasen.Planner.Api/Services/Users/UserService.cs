using Microsoft.EntityFrameworkCore;
using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Core;
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

        private IQueryable<User> Users => DbContext.Users
                .Include(u => u.Trainings)
                    .ThenInclude(t => t.ResourceType)
                .Include(u => u.Trainings)
                    .ThenInclude(t => t.ConfirmedByUser);

        public async Task<IEnumerable<UserResponse>> GetUsers()
        {
            var users = await Users
                .AsNoTracking()
                .Where(u => !u.Inactive)
                .AsNoTracking()
                .ToListAsync();
            return users.Select(Map).OrderBy(u => u.FullName).ToList();
        }

        public async Task<UserResponse?> GetUserById(int id)
        {
            var user = await Users
                .AsNoTracking()
                .SingleOrDefaultAsync(u => u.UserId == id);
            return user != null ? Map(user) : null;
        }

        public async Task<UserResponse> Create(UserRequest request)
        {
            var user = new User
            {
                FirstName = request.FirstName,
                LastName = request.LastName,
                UserName = request.PhoneNo,
                IsAdmin = request.IsAdmin ?? false,
                IsHidden = request.IsHidden ?? false,
            };
            DbContext.Users.Add(user);
            await DbContext.SaveChangesAsync();

            return Map(user);
        }

        public async Task<UserResponse?> Update(int id, UserRequest request)
        {
            var user = await DbContext.Users.SingleOrDefaultAsync(u => u.UserId == id);
            if (user == null) return null;

            if (!string.IsNullOrWhiteSpace(request.FirstName))
                user.FirstName = request.FirstName;
            if (!string.IsNullOrWhiteSpace(request.LastName))
                user.LastName = request.LastName;
            if (!string.IsNullOrWhiteSpace(request.PhoneNo))
                user.UserName = request.PhoneNo;
            if (request.IsAdmin.HasValue)
                user.IsAdmin = request.IsAdmin.Value;
            if (request.IsHidden.HasValue)
                user.IsHidden = request.IsHidden.Value;
            if (!string.IsNullOrWhiteSpace(request.Password))
            {
                var salt = PasswordHasher.CreateSalt();
                var password = PasswordHasher.HashPassword(request.Password, salt);
                user.Salt = salt;
                user.EncryptedPassword = password;
            }

            await DbContext.SaveChangesAsync();

            return Map(user);
        }

        public async Task<UserResponse?> Delete(int id)
        {
            var user = await DbContext.Users.SingleOrDefaultAsync(u => u.UserId == id);
            if (user == null) return null;

            user.Inactive = true;
            await DbContext.SaveChangesAsync();

            return Map(user);
        }

        public async Task<HallOfFameResponse> GetHallOfFame()
        {
            var hallOfFamers = await DbContext.HallOfFamers.ToListAsync();
            return Map(hallOfFamers);
        }

        public async Task<IEnumerable<PhoneResponse>> GetPhoneList()
        {
            var users = await DbContext.Users.Where(u => !u.Inactive && !u.IsHidden).AsNoTracking().ToListAsync();
            return users.Select(MapToPhone).OrderBy(u => u.FullName).ToList();
        }

        private PhoneResponse MapToPhone(User user)
        {
            return new PhoneResponse
            {
                Id = user.UserId,
                PhoneNo = user.UserName,
                FirstName = user.FirstName,
                LastName = user.LastName,
                FullName = MapFullName(user.FirstName, user.LastName),
            };
        }
        private UserResponse Map(User user) => new UserResponse
            {
                Id = user.UserId,
                PhoneNo = user.UserName,
                FirstName = user.FirstName,
                LastName = user.LastName,
                FullName = MapFullName(user.FirstName, user.LastName),
                IsAdmin = user.IsAdmin,
                IsHidden = user.IsHidden,
                Trainings = user.Trainings?.Select(Map).ToList() ?? new List<UserTrainingResponse>(),
            };

        private UserTrainingResponse Map(ResourceTypeTraining training) => new UserTrainingResponse
        {
            Id = training.ResourceTypeTrainingId,
            ResourceTypeId = training.ResourceTypeId,
            ResourceTypeName = training.ResourceType?.Name,
            TrainingComplete = training.TrainingComplete,
            Confirmed = training.Confirmed?.ToSimpleIsoString(),
            ConfirmedById = training.ConfirmedBy,
            ConfirmedByName = MapFullName(training.ConfirmedByUser?.FirstName, training.ConfirmedByUser?.LastName),
        };
        private HallOfFameResponse Map(IEnumerable<HallOfFamer> hallOfFamers)
        {
            var response = new HallOfFameResponse
            {
                HallOfFamers = hallOfFamers.Select(hof => new HallOfFamerResponse
                {
                    Id = hof.UserId,
                    FullName = MapFullName(hof.FirstName, hof.LastName),
                    Shifts = hof.Shifts,
                }).ToList(),
            };
            return response;
        }

        private string MapFullName(string? firstName, string? lastName)
        {
            return $"{firstName ?? ""} {lastName ?? ""}".Trim();
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
