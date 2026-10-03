using Microsoft.EntityFrameworkCore;
using Middagsasen.Planner.Api.Data;

namespace Middagsasen.Planner.Api.Services.Shifts
{
    public class ShiftRepository : IShiftRepository
    {
        /// <summary>Navnet på den unike indeksen på EventResourceUsers(EventResourceId, UserId).</summary>
        public const string UniqueShiftIndexName = "UQ_EventResourceUsers_EventResourceId_UserId";

        public ShiftRepository(PlannerDbContext dbContext)
        {
            DbContext = dbContext;
        }

        public PlannerDbContext DbContext { get; }

        /// <summary>
        /// Includes som <see cref="ResourceMapper"/> trenger. Brukes også av EventsService, så lesesiden og
        /// skriveoperasjonene laster det samme.
        /// </summary>
        public static IQueryable<EventResource> WithMappingIncludes(IQueryable<EventResource> resources) => resources
            .Include(r => r.Shifts)
                .ThenInclude(s => s.User)
                    .ThenInclude(u => u.Trainings)
            .Include(r => r.Shifts)
                .ThenInclude(s => s.User)
                    .ThenInclude(u => u.Competencies)
            .Include(r => r.ResourceType)
                .ThenInclude(rt => rt.Trainers)
                    .ThenInclude(t => t.User)
            .Include(r => r.ResourceType)
                .ThenInclude(rt => rt.Files)
            .Include(r => r.ResourceType)
                .ThenInclude(rt => rt.RequiredCompetencies)
                    .ThenInclude(rc => rc.Competency)
            .Include(r => r.Messages)
                .ThenInclude(m => m.CreatedByUser);

        public async Task<T> InResourceLock<T>(int resourceId, Func<Task<T>> work)
        {
            await using var transaction = await DbContext.Database.BeginTransactionAsync();

            // Låser ressursraden ved å oppdatere en kolonne til sin egen verdi. En UPDATE tar eksklusiv radlås som
            // holdes til commit både i SQL Server og PostgreSQL, uten databasespesifikk SQL (som UPDLOCK-hint eller
            // SELECT ... FOR UPDATE). En samtidig endring på samme ressurs venter derfor her til denne er ferdig,
            // og leser deretter vaktene på nytt.
            var locked = await DbContext.EventResource
                .Where(r => r.EventResourceId == resourceId)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.MinimumStaff, r => r.MinimumStaff));

            if (locked == 0)
                throw new EntityNotFoundException("Fant ikke vaktressursen.");

            var result = await work();
            await transaction.CommitAsync();
            return result;
        }

        public async Task<EventResource?> GetResource(int resourceId)
        {
            return await WithMappingIncludes(DbContext.EventResource)
                .AsNoTracking()
                .AsSplitQuery()
                .SingleOrDefaultAsync(r => r.EventResourceId == resourceId);
        }

        public async Task<EventResourceUser?> GetShift(int shiftId)
        {
            return await DbContext.Shifts.SingleOrDefaultAsync(s => s.EventResourceUserId == shiftId);
        }

        public async Task<int?> GetResourceIdForShift(int shiftId)
        {
            return await DbContext.Shifts
                .Where(s => s.EventResourceUserId == shiftId)
                .Select(s => (int?)s.EventResourceId)
                .SingleOrDefaultAsync();
        }

        public async Task<bool> UserExists(int userId)
        {
            return await DbContext.Users.AnyAsync(u => u.UserId == userId);
        }

        public async Task<ResourceTypeTraining?> GetTraining(int userId, int resourceTypeId)
        {
            return await DbContext.ResourceTypeTrainings
                .SingleOrDefaultAsync(t => t.UserId == userId && t.ResourceTypeId == resourceTypeId);
        }

        public async Task<ResourceTypeTraining> GetTrainingForResponse(int trainingId)
        {
            return await DbContext.ResourceTypeTrainings
                .Include(t => t.ResourceType)
                .Include(t => t.ConfirmedByUser)
                .AsNoTracking()
                .SingleAsync(t => t.ResourceTypeTrainingId == trainingId);
        }

        public async Task<IReadOnlyList<int>> GetTrainingResourceTypeIds(int userId)
        {
            return await DbContext.ResourceTypeTrainings
                .Where(t => t.UserId == userId)
                .Select(t => t.ResourceTypeId)
                .ToListAsync();
        }

        public void AddShift(EventResourceUser shift) => DbContext.Shifts.Add(shift);

        public void RemoveShift(EventResourceUser shift) => DbContext.Shifts.Remove(shift);

        public void AddTraining(ResourceTypeTraining training) => DbContext.ResourceTypeTrainings.Add(training);

        public async Task SaveChangesAsync()
        {
            try
            {
                await DbContext.SaveChangesAsync();
            }
            // Både SQL Server og PostgreSQL tar med indeksnavnet i feilmeldingen, så dette er databaseuavhengig.
            catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains(UniqueShiftIndexName) == true)
            {
                throw new DomainValidationException(ShiftService.DuplicateMessage);
            }
        }
    }
}
