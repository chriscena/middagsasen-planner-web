using Microsoft.EntityFrameworkCore;
using Middagsasen.Planner.Api.Data;

namespace Middagsasen.Planner.Api.Services.Shifts
{
    /// <summary>Oppslagene <see cref="TrainerNotifier"/> trenger. Alt hentes uten tracking.</summary>
    public interface ITrainerRepository
    {
        /// <exception cref="InvalidOperationException">Brukeren finnes ikke.</exception>
        Task<User> GetUser(int userId);

        /// <exception cref="InvalidOperationException">Ressurstypen finnes ikke.</exception>
        Task<ResourceType> GetResourceType(int resourceTypeId);

        /// <summary>Trenerne (brukerne) for ressurstypen.</summary>
        Task<IReadOnlyList<User>> GetTrainers(int resourceTypeId);
    }

    public class TrainerRepository : ITrainerRepository
    {
        public TrainerRepository(PlannerDbContext dbContext)
        {
            DbContext = dbContext;
        }

        public PlannerDbContext DbContext { get; }

        public async Task<User> GetUser(int userId)
        {
            return await DbContext.Users.AsNoTracking().SingleAsync(u => u.UserId == userId);
        }

        public async Task<ResourceType> GetResourceType(int resourceTypeId)
        {
            return await DbContext.ResourceTypes.AsNoTracking().SingleAsync(t => t.ResourceTypeId == resourceTypeId);
        }

        public async Task<IReadOnlyList<User>> GetTrainers(int resourceTypeId)
        {
            return await DbContext.ResourceTypeTrainers
                .AsNoTracking()
                .Where(t => t.ResourceTypeId == resourceTypeId)
                .Select(t => t.User)
                .ToListAsync();
        }
    }
}
