using Microsoft.EntityFrameworkCore;
using Middagsasen.Planner.Api.Core;
using Middagsasen.Planner.Api.Data;

namespace Middagsasen.Planner.Api.Services.Attachments
{
    public class AttachmentRepository : IAttachmentRepository
    {
        /// <summary>Navnet på den unike indeksen på ResourceTypeFileAcknowledgements(UserId, ResourceTypeFileId).</summary>
        public const string UniqueAcknowledgementIndexName = "UQ_ResourceTypeFileAcknowledgements_UserId_ResourceTypeFileId";

        public AttachmentRepository(PlannerDbContext dbContext)
        {
            DbContext = dbContext;
        }

        public PlannerDbContext DbContext { get; }

        private IQueryable<ResourceTypeFile> FilesWithNames => DbContext.ResourceTypeFiles
            .Include(f => f.CreatedByUser)
            .Include(f => f.UpdatedByUser);

        public async Task<bool> ResourceTypeExists(int resourceTypeId)
        {
            return await DbContext.ResourceTypes.AnyAsync(r => r.ResourceTypeId == resourceTypeId);
        }

        public async Task<ResourceTypeFile?> Get(int fileId, int resourceTypeId)
        {
            return await FilesWithNames
                .SingleOrDefaultAsync(f => f.ResourceTypeFileId == fileId && f.ResourceTypeId == resourceTypeId);
        }

        public async Task<IReadOnlyList<ResourceTypeFile>> GetArchived(int resourceTypeId)
        {
            return await FilesWithNames
                .AsNoTracking()
                .Where(f => f.ResourceTypeId == resourceTypeId && f.Archived != null)
                .OrderByDescending(f => f.Archived).ThenByDescending(f => f.ResourceTypeFileId)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<ResourceTypeFile>> GetActiveInstructions()
        {
            return await FilesWithNames
                .Include(f => f.ResourceType)
                .AsNoTracking()
                .Where(f => f.Archived == null && f.RequiresAcknowledgement && !f.ResourceType.Inactive)
                .OrderBy(f => f.ResourceTypeFileId)
                .ToListAsync();
        }

        public async Task<ResourceTypeFile> Add(ResourceTypeFile file)
        {
            DbContext.ResourceTypeFiles.Add(file);
            await DbContext.SaveChangesAsync();
            return file;
        }

        public async Task SaveChangesAsync()
        {
            await DbContext.SaveChangesAsync();
        }

        public async Task<ResourceTypeFileAcknowledgement?> GetAcknowledgement(int fileId, int userId)
        {
            return await DbContext.ResourceTypeFileAcknowledgements
                .AsNoTracking()
                .SingleOrDefaultAsync(a => a.ResourceTypeFileId == fileId && a.UserId == userId);
        }

        public async Task<ResourceTypeFileAcknowledgement> Acknowledge(ResourceTypeFileAcknowledgement acknowledgement)
        {
            DbContext.ResourceTypeFileAcknowledgements.Add(acknowledgement);
            try
            {
                await DbContext.SaveChangesAsync();
                return acknowledgement;
            }
            // Både SQL Server og PostgreSQL tar med indeksnavnet i feilmeldingen, så dette er databaseuavhengig.
            catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains(UniqueAcknowledgementIndexName) == true)
            {
                DbContext.Entry(acknowledgement).State = EntityState.Detached;
                return await GetAcknowledgement(acknowledgement.ResourceTypeFileId, acknowledgement.UserId)
                    ?? throw new InvalidOperationException("Kvitteringen finnes ikke selv om den unike indeksen slo til.");
            }
        }

        public async Task<IReadOnlyList<ResourceTypeFileAcknowledgement>> GetAcknowledgements(int fileId)
        {
            return await DbContext.ResourceTypeFileAcknowledgements
                .Include(a => a.User)
                .AsNoTracking()
                .Where(a => a.ResourceTypeFileId == fileId)
                .ToListAsync();
        }

        public async Task<IReadOnlyDictionary<int, DateTime>> GetAcknowledgedFileIds(int userId)
        {
            return await DbContext.ResourceTypeFileAcknowledgements
                .AsNoTracking()
                .Where(a => a.UserId == userId)
                .ToDictionaryAsync(a => a.ResourceTypeFileId, a => a.Acknowledged);
        }

        public async Task<IReadOnlyList<ResourceTypeFile>> GetUnacknowledgedMandatory(int userId, int resourceTypeId)
        {
            var files = await DbContext.ResourceTypeFiles
                .AsNoTracking()
                .Where(f => f.ResourceTypeId == resourceTypeId && f.Archived == null)
                .ToListAsync();
            var acknowledgedFileIds = await DbContext.ResourceTypeFileAcknowledgements
                .AsNoTracking()
                .Where(a => a.UserId == userId && a.File.ResourceTypeId == resourceTypeId)
                .Select(a => a.ResourceTypeFileId)
                .ToListAsync();
            // Regelen og sorteringen bor ett sted, så feilmeldingen peker på samme instruks som flaggene.
            return AttachmentRules.BlockingInstructions(files, acknowledgedFileIds.ToHashSet());
        }

        public async Task<IReadOnlySet<int>> GetResourceTypesWithShiftsSince(int userId, DateTime fromUtc)
        {
            var from = ToResourceTime(fromUtc);
            var ids = await DbContext.Shifts
                .AsNoTracking()
                .Where(s => s.UserId == userId && s.Resource.StartTime >= from)
                .Select(s => s.Resource.ResourceTypeId)
                .Distinct()
                .ToListAsync();
            return ids.ToHashSet();
        }

        public async Task<IReadOnlySet<int>> GetUsersWithShiftsSince(int resourceTypeId, DateTime fromUtc)
        {
            var from = ToResourceTime(fromUtc);
            var ids = await DbContext.Shifts
                .AsNoTracking()
                .Where(s => s.Resource.ResourceTypeId == resourceTypeId && s.Resource.StartTime >= from && !s.User.Inactive)
                .Select(s => s.UserId)
                .Distinct()
                .ToListAsync();
            return ids.ToHashSet();
        }

        public async Task<IReadOnlyList<User>> GetActiveUsers(IReadOnlySet<int>? userIds = null)
        {
            var query = DbContext.Users
                .AsNoTracking()
                .Where(u => !u.Inactive);
            if (userIds is not null)
            {
                if (userIds.Count == 0)
                    return [];
                var ids = userIds.ToList();
                query = query.Where(u => ids.Contains(u.UserId));
            }
            return await query.ToListAsync();
        }

        /// <summary>Oppgavenes tider lagres som norsk lokal tid uten sone, så et UTC-tidspunkt må konverteres før sammenligning.</summary>
        private static DateTime ToResourceTime(DateTime utc) => new DateTimeOffset(utc.AsUtc()).ToNorwegianLocalTime();
    }
}
