using Microsoft.EntityFrameworkCore;
using Middagsasen.Planner.Api.Core;
using Middagsasen.Planner.Api.Data;

namespace Middagsasen.Planner.Api.Services.Users
{
    public class UserService : IUserService
    {
        public UserService(PlannerDbContext dbContext)
        {
            DbContext = dbContext;
        }

        public PlannerDbContext DbContext { get; }

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

        /// <summary>
        /// Oppretter en ny bruker. Telefonnummeret normaliseres til samme format som lagres og slås opp ved innlogging.
        /// </summary>
        /// <remarks>
        /// Hvis nummeret (normalisert) tilhører en inaktiv bruker (slettet via <see cref="Delete"/>), reaktiveres
        /// den i stedet for at en ny bruker opprettes. Samme ID beholdes, og:
        /// <list type="bullet">
        /// <item><c>Inactive</c> settes til <c>false</c>.</item>
        /// <item>Fornavn og etternavn oppdateres når de er oppgitt (ellers beholdes de gamle).</item>
        /// <item><c>IsAdmin</c> og <c>IsHidden</c> settes fra requesten som for en ny bruker (standard <c>false</c>).</item>
        /// <item>Passordet settes når det er oppgitt.</item>
        /// <item><c>ShiftReminders</c> settes til <c>false</c> (som for en ny bruker); brukeren slår det på selv igjen.</item>
        /// </list>
        /// <see cref="Delete"/> setter bare <c>Inactive</c> (og logger ut), så opplæringer, vakter og annen historikk følger med tilbake.
        /// Er nummeret i bruk av en aktiv bruker, avvises det med <see cref="DomainValidationException"/>. Det gjelder
        /// også når en parallell forespørsel tar nummeret mellom sjekken og lagringen (den unike indeksen avviser da lagringen).
        /// </remarks>
        public async Task<UserResponse> Create(UserRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.PhoneNo))
                throw new DomainValidationException(PhoneNoRequiredMessage);

            var userName = request.PhoneNo.ToNormalizedUserName()
                ?? throw new DomainValidationException(PhoneNoInvalidMessage);

            var user = await DbContext.Users.WhereUserName(userName).SingleOrDefaultAsync();

            if (user is { Inactive: true })
            {
                user.Inactive = false;
                if (!string.IsNullOrWhiteSpace(request.FirstName))
                    user.FirstName = request.FirstName;
                if (!string.IsNullOrWhiteSpace(request.LastName))
                    user.LastName = request.LastName;
                user.IsAdmin = request.IsAdmin ?? false;
                user.IsHidden = request.IsHidden ?? false;
                user.ShiftReminders = false;
                SetPassword(user, request.Password);
            }
            else if (user != null)
            {
                throw new DomainValidationException(PhoneNoInUseMessage);
            }
            else
            {
                user = new User
                {
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    UserName = userName,
                    IsAdmin = request.IsAdmin ?? false,
                    IsHidden = request.IsHidden ?? false,
                };
                DbContext.Users.Add(user);
            }

            // En ny bruker har ikke fått ID ennå, så den ekskluderer ingen andre brukere.
            await SaveChangesCheckingUserName(userName, user.UserId);

            return await GetUserById(user.UserId);
        }

        /// <summary>
        /// Oppdaterer en bruker (administrator). Telefonnummeret valideres, sjekkes mot andre brukere og lagres
        /// bare når det faktisk endres, det vil si når <c>PhoneNo</c> er satt og verken er lik lagret brukernavn
        /// direkte eller normalisert lik det. Ellers står brukernavnet urørt, også når det ikke er et gyldig
        /// telefonnummer (f.eks. eldre brukere med brukernavn «admin»).
        /// </summary>
        public async Task<UserResponse> Update(int id, UserRequest request)
        {
            var user = await DbContext.Users.SingleOrDefaultAsync(u => u.UserId == id)
                ?? throw new EntityNotFoundException($"Fant ikke bruker med ID {id}");

            ApplyCommonFields(user, request.FirstName, request.LastName, request.Password);
            if (IsPhoneNoChanged(user.UserName, request.PhoneNo))
                user.UserName = await GetAvailableUserName(request.PhoneNo!, user.UserId);
            if (request.IsAdmin.HasValue)
                user.IsAdmin = request.IsAdmin.Value;
            if (request.IsHidden.HasValue)
                user.IsHidden = request.IsHidden.Value;

            await SaveChangesCheckingUserName(user.UserName, user.UserId);

            return await GetUserById(user.UserId);
        }

        /// <summary>
        /// Oppdaterer innlogget bruker. Brukernavn (telefonnummer) og admin kan ikke endres her.
        /// </summary>
        public async Task<UserResponse> UpdateMe(int userId, UpdateMeRequest request)
        {
            var user = await DbContext.Users.SingleOrDefaultAsync(u => u.UserId == userId)
                ?? throw new EntityNotFoundException($"Fant ikke bruker med ID {userId}");

            ApplyCommonFields(user, request.FirstName, request.LastName, request.Password);
            if (request.IsHidden.HasValue)
                user.IsHidden = request.IsHidden.Value;
            if (request.ShiftReminders.HasValue)
                user.ShiftReminders = request.ShiftReminders.Value;

            await DbContext.SaveChangesAsync();

            return await GetUserById(user.UserId);
        }

        /// <summary>
        /// Felt som både brukeren selv og administrator kan endre. Tomme verdier ignoreres.
        /// </summary>
        private static void ApplyCommonFields(User user, string? firstName, string? lastName, string? password)
        {
            if (!string.IsNullOrWhiteSpace(firstName))
                user.FirstName = firstName;
            if (!string.IsNullOrWhiteSpace(lastName))
                user.LastName = lastName;
            SetPassword(user, password);
        }

        private static void SetPassword(User user, string? password)
        {
            if (string.IsNullOrWhiteSpace(password))
                return;
            var salt = PasswordHasher.CreateSalt();
            user.Salt = salt;
            user.EncryptedPassword = PasswordHasher.HashPassword(password, salt);
        }

        /// <summary>
        /// Nummeret regnes som endret når det er satt og verken er lik lagret brukernavn direkte
        /// eller normalisert lik det.
        /// </summary>
        private static bool IsPhoneNoChanged(string storedUserName, string? phoneNo)
        {
            if (string.IsNullOrWhiteSpace(phoneNo) || phoneNo == storedUserName)
                return false;
            var normalized = phoneNo.ToNormalizedUserName();
            return normalized == null || normalized != storedUserName.ToNormalizedUserName();
        }

        /// <summary>
        /// Normaliserer telefonnummeret med <see cref="UserNameExtensions.ToNormalizedUserName"/> (samme format som
        /// lagres og slås opp ved innlogging/OTP), og avviser det hvis det er ugyldig eller allerede brukes av en
        /// annen bruker. Inaktive brukere telles med, siden OTP-innlogging slår opp brukernavn uten å filtrere på Inactive.
        /// </summary>
        private async Task<string> GetAvailableUserName(string phoneNo, int excludeUserId)
        {
            var userName = phoneNo.ToNormalizedUserName()
                ?? throw new DomainValidationException(PhoneNoInvalidMessage);

            if (await IsUserNameInUse(userName, excludeUserId))
                throw new DomainValidationException(PhoneNoInUseMessage);

            return userName;
        }

        /// <summary>
        /// Om en annen bruker (også inaktiv) har brukernavnet. Lagrede brukernavn er normalisert
        /// (eldre data normaliseres av Script.PreDeployment.sql i databaseprosjektet), så eksakt oppslag holder.
        /// </summary>
        private Task<bool> IsUserNameInUse(string normalizedUserName, int excludeUserId)
            => DbContext.Users.WhereUserName(normalizedUserName).AnyAsync(u => u.UserId != excludeUserId);

        /// <summary>
        /// Lagrer, og gjør om brudd på den unike indeksen på <c>Users.UserName</c> til
        /// <see cref="DomainValidationException"/>. Det skjer når en parallell forespørsel tar nummeret mellom
        /// sjekken og lagringen.
        /// </summary>
        private async Task SaveChangesCheckingUserName(string userName, int userId)
        {
            if (!await DbContext.TrySaveWithUniqueUserName(userName, userId))
                throw new DomainValidationException(PhoneNoInUseMessage);
        }

        public async Task<UserResponse> Delete(int id)
        {
            var user = await DbContext.Users.SingleOrDefaultAsync(u => u.UserId == id)
                ?? throw new EntityNotFoundException($"Fant ikke bruker med ID {id}");

            user.Inactive = true;
            // Slett sesjonene i samme lagring, så deaktivering logger brukeren ut umiddelbart.
            DbContext.UserSessions.RemoveRange(await DbContext.UserSessions.Where(s => s.UserId == id).ToListAsync());
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
                FullName = user.FullName(),
            };
        }
        private UserResponse Map(User user) => new UserResponse
            {
                Id = user.UserId,
                PhoneNo = user.UserName,
                FirstName = user.FirstName,
                LastName = user.LastName,
                FullName = user.FullName(),
                IsAdmin = user.IsAdmin,
                IsHidden = user.IsHidden,
                ShiftReminders = user.ShiftReminders,
                Trainings = user.Trainings.Select(Map).ToList(),
            };

        private UserTrainingResponse Map(ResourceTypeTraining training) => new UserTrainingResponse
        {
            Id = training.ResourceTypeTrainingId,
            ResourceTypeId = training.ResourceTypeId,
            ResourceTypeName = training.ResourceType?.Name,
            TrainingComplete = training.TrainingComplete,
            Confirmed = training.Confirmed?.AsUtc().ToIsoString(),
            ConfirmedById = training.ConfirmedBy,
            ConfirmedByName = NameExtensions.FullName(training.ConfirmedByUser?.FirstName, training.ConfirmedByUser?.LastName),
        };
        private HallOfFameResponse Map(IEnumerable<HallOfFamer> hallOfFamers)
        {
            var response = new HallOfFameResponse
            {
                HallOfFamers = hallOfFamers.Select(hof => new HallOfFamerResponse
                {
                    Id = hof.UserId,
                    FullName = NameExtensions.FullName(hof.FirstName, hof.LastName),
                    Shifts = hof.Shifts,
                }).ToList(),
            };
            return response;
        }
    }
}
