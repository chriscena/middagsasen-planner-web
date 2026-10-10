using Microsoft.EntityFrameworkCore;
using Middagsasen.Planner.Api.Core;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services.SmsSender;

namespace Middagsasen.Planner.Api.Services.Shifts
{
    /// <summary>
    /// Oppslagene <see cref="TrainerNotifier"/> trenger. Alt hentes uten tracking, og brukere projiseres til det
    /// SMS-en trenger (navn og telefonnummer) i stedet for hele <see cref="User"/>. Går ikke via
    /// <see cref="Users.IUserService.GetUserById"/>, som laster opplæringene med vakttyper og bekreftere for et svar
    /// vi bare skulle lese navnet fra.
    /// </summary>
    public interface ITrainerRepository
    {
        /// <summary>Navnet til brukeren som ønsker opplæring.</summary>
        /// <exception cref="InvalidOperationException">Brukeren finnes ikke.</exception>
        Task<PersonName> GetName(int userId);

        /// <exception cref="InvalidOperationException">Vakttypen finnes ikke.</exception>
        Task<ResourceType> GetResourceType(int resourceTypeId);

        /// <summary>Trenerne for vakttypen, som SMS-mottakere.</summary>
        Task<IReadOnlyList<SmsRecipient>> GetTrainers(int resourceTypeId);
    }

    public class TrainerRepository : ITrainerRepository
    {
        public TrainerRepository(PlannerDbContext dbContext)
        {
            DbContext = dbContext;
        }

        public PlannerDbContext DbContext { get; }

        public async Task<PersonName> GetName(int userId)
        {
            return await DbContext.Users
                .AsNoTracking()
                .Where(u => u.UserId == userId)
                .Select(u => new PersonName(u.FirstName, u.LastName))
                .SingleAsync();
        }

        public async Task<ResourceType> GetResourceType(int resourceTypeId)
        {
            return await DbContext.ResourceTypes.AsNoTracking().SingleAsync(t => t.ResourceTypeId == resourceTypeId);
        }

        public async Task<IReadOnlyList<SmsRecipient>> GetTrainers(int resourceTypeId)
        {
            return await DbContext.ResourceTypeTrainers
                .AsNoTracking()
                .Where(t => t.ResourceTypeId == resourceTypeId)
                .Select(t => new SmsRecipient(t.User.UserId, t.User.UserName, t.User.FirstName))
                .ToListAsync();
        }
    }
}
