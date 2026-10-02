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

        public async Task<UserResponse> GetUserById(int id)
        {
            var user = await Users
                .AsNoTracking()
                .SingleOrDefaultAsync(u => u.UserId == id)
                ?? throw new EntityNotFoundException($"Fant ikke bruker med ID {id}");
            return Map(user);
        }

        internal const string PhoneNoInUseMessage = "Telefonnummeret er allerede i bruk.";
        internal const string PhoneNoInvalidMessage = "Telefonnummeret er ugyldig.";
        internal const string PhoneNoRequiredMessage = "Telefonnummer må fylles ut.";

        public async Task<UserResponse> Create(UserRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.PhoneNo))
                throw new DomainValidationException(PhoneNoRequiredMessage);

            var userName = await GetAvailableUserName(request.PhoneNo, excludeUserId: null);

            var user = new User
            {
                FirstName = request.FirstName,
                LastName = request.LastName,
                UserName = userName,
                IsAdmin = request.IsAdmin ?? false,
                IsHidden = request.IsHidden ?? false,
            };
            DbContext.Users.Add(user);
            await DbContext.SaveChangesAsync();

            return Map(user);
        }

        public async Task<UserResponse> Update(int id, UserRequest request)
        {
            var user = await DbContext.Users.SingleOrDefaultAsync(u => u.UserId == id)
                ?? throw new EntityNotFoundException($"Fant ikke bruker med ID {id}");

            await ApplyCommonFields(user, request.FirstName, request.LastName, request.PhoneNo, request.Password);
            if (request.IsAdmin.HasValue)
                user.IsAdmin = request.IsAdmin.Value;
            if (request.IsHidden.HasValue)
                user.IsHidden = request.IsHidden.Value;

            await DbContext.SaveChangesAsync();

            return Map(user);
        }

        public async Task<UserResponse> UpdateMe(int userId, UpdateMeRequest request)
        {
            var user = await DbContext.Users.SingleOrDefaultAsync(u => u.UserId == userId)
                ?? throw new EntityNotFoundException($"Fant ikke bruker med ID {userId}");

            await ApplyCommonFields(user, request.FirstName, request.LastName, request.PhoneNo, request.Password);
            if (request.IsHidden.HasValue)
                user.IsHidden = request.IsHidden.Value;

            await DbContext.SaveChangesAsync();

            return Map(user);
        }

        /// <summary>
        /// Felt som både brukeren selv og administrator kan endre. Tomme verdier ignoreres.
        /// </summary>
        private async Task ApplyCommonFields(User user, string? firstName, string? lastName, string? phoneNo, string? password)
        {
            if (!string.IsNullOrWhiteSpace(firstName))
                user.FirstName = firstName;
            if (!string.IsNullOrWhiteSpace(lastName))
                user.LastName = lastName;
            if (!string.IsNullOrWhiteSpace(phoneNo))
                user.UserName = await GetAvailableUserName(phoneNo, user.UserId);
            if (!string.IsNullOrWhiteSpace(password))
            {
                var salt = PasswordHasher.CreateSalt();
                var hash = PasswordHasher.HashPassword(password, salt);
                user.Salt = salt;
                user.EncryptedPassword = hash;
            }
        }

        /// <summary>
        /// Normaliserer telefonnummeret til samme format som brukes ved innlogging (8 siffer uten landkode),
        /// og avviser nummeret hvis det er ugyldig eller allerede brukes av en annen bruker.
        /// Inaktive brukere telles med, siden OTP-innlogging slår opp brukernavn uten å filtrere på Inactive.
        /// </summary>
        private async Task<string> GetAvailableUserName(string phoneNo, int? excludeUserId)
        {
            var phoneNumber = phoneNo.ToNumericPhoneNo();
            if (phoneNumber == 0)
                throw new DomainValidationException(PhoneNoInvalidMessage);

            // Eldre data kan ha brukernavn i andre formater (f.eks. "+47 ..."), så sammenligningen gjøres
            // på normalisert nummer. Brukertabellen er liten, så det er greit å hente alle brukernavnene.
            var otherUserNames = await DbContext.Users
                .AsNoTracking()
                .Where(u => excludeUserId == null || u.UserId != excludeUserId)
                .Select(u => u.UserName)
                .ToListAsync();

            if (otherUserNames.Any(existing => existing.ToNumericPhoneNo() == phoneNumber))
                throw new DomainValidationException(PhoneNoInUseMessage);

            return phoneNumber.ToUserName();
        }

        public async Task<UserResponse> Delete(int id)
        {
            var user = await DbContext.Users.SingleOrDefaultAsync(u => u.UserId == id)
                ?? throw new EntityNotFoundException($"Fant ikke bruker med ID {id}");

            user.Inactive = true;
            await DbContext.SaveChangesAsync();

            return Map(user);
        }

        public async Task<HallOfFameResponse> GetHallOfFame()
        {
            var hallOfFamers = await DbContext.HallOfFamers
                .OrderByDescending(h => h.Shifts)
                .ThenBy(h => h.FirstName)
                .ToListAsync();
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
