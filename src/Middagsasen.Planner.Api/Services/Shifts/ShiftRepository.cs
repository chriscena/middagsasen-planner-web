using Microsoft.EntityFrameworkCore;
using Middagsasen.Planner.Api.Data;

namespace Middagsasen.Planner.Api.Services.Shifts
{
    public class ShiftRepository : IShiftRepository
    {
        /// <summary>Navnet på den unike indeksen på EventResourceUsers(EventResourceId, UserId).</summary>
        public const string UniqueShiftIndexName = "UQ_EventResourceUsers_EventResourceId_UserId";

        /// <summary>Navnet på den unike indeksen på ResourceTypeTrainings(UserId, ResourceTypeId).</summary>
        public const string UniqueTrainingIndexName = "UQ_ResourceTypeTrainings_UserId_ResourceTypeId";

        public ShiftRepository(PlannerDbContext dbContext)
        {
            DbContext = dbContext;
        }

        public PlannerDbContext DbContext { get; }

        public async Task<T> InResourceLock<T>(int resourceId, Func<Task<T>> work)
        {
            await using var transaction = await DbContext.Database.BeginTransactionAsync();

            // En samtidig endring på samme ressurs (også lagring av vaktlisteskjemaet, se EventsService.UpdateEvent)
            // venter her til denne er ferdig, og leser deretter vaktene på nytt. Se RowLocks for teknikken.
            if (!await DbContext.LockResource(resourceId))
                throw new EntityNotFoundException("Fant ikke vaktressursen.");

            var result = await work();
            await transaction.CommitAsync();
            return result;
        }

        public async Task<EventResource?> GetResource(int resourceId)
        {
            return await DbContext.EventResource
                .Include(r => r.ResourceType)
                    .ThenInclude(t => t.Trainers)
                .Include(r => r.Shifts)
                    .ThenInclude(s => s.User)
                    .ThenInclude(u => u.Trainings)
                .AsNoTracking()
                .AsSplitQuery()
                .SingleOrDefaultAsync(r => r.EventResourceId == resourceId);
        }

        public async Task<ResourceStaffing> GetStaffing(int resourceId)
        {
            return await DbContext.EventResource
                .Where(r => r.EventResourceId == resourceId)
                .Select(r => new ResourceStaffing(r.ShiftCount, r.Shifts.Count()))
                .SingleAsync();
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

        public async Task SetShiftCount(int resourceId, int shiftCount)
        {
            await DbContext.EventResource
                .Where(r => r.EventResourceId == resourceId)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.ShiftCount, shiftCount));
        }

        public void AddShift(EventResourceUser shift) => DbContext.Shifts.Add(shift);

        public void RemoveShift(EventResourceUser shift) => DbContext.Shifts.Remove(shift);

        public void AddTraining(ResourceTypeTraining training) => DbContext.ResourceTypeTrainings.Add(training);

        public void DiscardChanges() => DbContext.ChangeTracker.Clear();

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
            catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains(UniqueTrainingIndexName) == true)
            {
                throw new TrainingConflictException(ex);
            }
        }
    }
}
