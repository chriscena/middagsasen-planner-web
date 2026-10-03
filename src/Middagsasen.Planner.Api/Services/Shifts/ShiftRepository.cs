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

        /// <summary>Navnet på fremmednøkkelen WorkHours.ShiftId → EventResourceUsers (uten cascade).</summary>
        public const string WorkHoursShiftForeignKeyName = "FK_WorkHours_Users_ShiftId";

        public ShiftRepository(PlannerDbContext dbContext)
        {
            DbContext = dbContext;
        }

        public PlannerDbContext DbContext { get; }

        /// <summary>
        /// Navigasjonene <see cref="ResourceMapper"/> trenger, relativt til en <see cref="EventResource"/>. Én liste brukes
        /// både for ressurser og for events (med prefikset <c>Resources</c>), så lesesiden og skriveoperasjonene laster det samme.
        /// </summary>
        private static readonly string[] MappingIncludePaths =
        [
            Path(nameof(EventResource.Shifts), nameof(EventResourceUser.User), nameof(User.Trainings)),
            Path(nameof(EventResource.Shifts), nameof(EventResourceUser.User), nameof(User.Competencies)),
            Path(nameof(EventResource.Shifts), nameof(EventResourceUser.WorkHours)),
            Path(nameof(EventResource.ResourceType), nameof(ResourceType.Trainers), nameof(ResourceTypeTrainer.User)),
            Path(nameof(EventResource.ResourceType), nameof(ResourceType.Files)),
            Path(nameof(EventResource.ResourceType), nameof(ResourceType.RequiredCompetencies), nameof(ResourceTypeCompetency.Competency)),
            Path(nameof(EventResource.Messages), nameof(EventResourceMessage.CreatedByUser)),
        ];

        private static string Path(params string[] navigations) => string.Join('.', navigations);

        private static IQueryable<T> IncludeMappingPaths<T>(IQueryable<T> query, string? prefix) where T : class
            => MappingIncludePaths.Aggregate(query, (q, path) => q.Include(prefix is null ? path : Path(prefix, path)));

        /// <summary>Includes som <see cref="ResourceMapper"/> trenger for ressursen.</summary>
        public static IQueryable<EventResource> WithMappingIncludes(IQueryable<EventResource> resources)
            => IncludeMappingPaths(resources, null);

        /// <summary>Includes som <see cref="ResourceMapper"/> trenger for ressursene til eventene (lesesiden i EventsService).</summary>
        public static IQueryable<Event> WithMappingIncludes(IQueryable<Event> events)
            => IncludeMappingPaths(events, nameof(Event.Resources));

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

        public async Task SetMinimumStaff(int resourceId, int minimumStaff)
        {
            await DbContext.EventResource
                .Where(r => r.EventResourceId == resourceId)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.MinimumStaff, minimumStaff));
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
            // Både SQL Server og PostgreSQL tar med navnet på indeksen/fremmednøkkelen i feilmeldingen, så dette er databaseuavhengig.
            catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains(UniqueShiftIndexName) == true)
            {
                throw new DomainValidationException(ShiftService.DuplicateMessage);
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains(UniqueTrainingIndexName) == true)
            {
                throw new TrainingConflictException(ex);
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains(WorkHoursShiftForeignKeyName) == true)
            {
                throw new DomainValidationException(ShiftService.HasWorkHoursMessage);
            }
        }
    }
}
