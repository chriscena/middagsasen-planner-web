using Microsoft.EntityFrameworkCore;
using Middagsasen.Planner.Api.Data;

namespace Middagsasen.Planner.Api.Services.Competencies
{
    public class CompetencyRepository : ICompetencyRepository
    {
        public CompetencyRepository(PlannerDbContext dbContext)
        {
            DbContext = dbContext;
        }

        public PlannerDbContext DbContext { get; }

        // Competencies

        public async Task<IEnumerable<Competency>> GetCompetencies()
        {
            return await DbContext.Competencies
                .Include(c => c.ResourceTypeCompetencies)
                    .ThenInclude(rtc => rtc.ResourceType)
                .Include(c => c.CompetencyApprovers)
                    .ThenInclude(ca => ca.User)
                .AsNoTracking()
                .Where(c => !c.Inactive)
                .ToListAsync();
        }

        public async Task<Competency?> GetCompetencyById(int id)
        {
            return await DbContext.Competencies
                .Include(c => c.ResourceTypeCompetencies)
                    .ThenInclude(rtc => rtc.ResourceType)
                .Include(c => c.CompetencyApprovers)
                    .ThenInclude(ca => ca.User)
                .SingleOrDefaultAsync(c => c.CompetencyId == id);
        }

        public async Task<Competency> AddCompetency(Competency competency)
        {
            DbContext.Competencies.Add(competency);
            await DbContext.SaveChangesAsync();
            return competency;
        }

        public async Task SaveChangesAsync()
        {
            await DbContext.SaveChangesAsync();
        }

        // User competencies

        public async Task<IEnumerable<UserCompetency>> GetUserCompetencies(int userId)
        {
            return await DbContext.UserCompetencies
                .Include(uc => uc.User)
                .Include(uc => uc.Competency)
                .Include(uc => uc.ApprovedByUser)
                .AsNoTracking()
                .Where(uc => uc.UserId == userId)
                .ToListAsync();
        }

        public async Task<UserCompetency?> GetUserCompetencyById(int id)
        {
            return await DbContext.UserCompetencies
                .Include(uc => uc.User)
                .Include(uc => uc.Competency)
                .Include(uc => uc.ApprovedByUser)
                .SingleOrDefaultAsync(uc => uc.UserCompetencyId == id);
        }

        public async Task<UserCompetency> AddUserCompetency(UserCompetency userCompetency)
        {
            DbContext.UserCompetencies.Add(userCompetency);
            await DbContext.SaveChangesAsync();
            return userCompetency;
        }

        public async Task RemoveUserCompetency(UserCompetency userCompetency)
        {
            DbContext.UserCompetencies.Remove(userCompetency);
            await DbContext.SaveChangesAsync();
        }

        // Approvers

        public async Task<IEnumerable<CompetencyApprover>> GetApprovers(int competencyId)
        {
            return await DbContext.CompetencyApprovers
                .Include(ca => ca.User)
                .AsNoTracking()
                .Where(ca => ca.CompetencyId == competencyId && !ca.Inactive)
                .ToListAsync();
        }

        public async Task<CompetencyApprover?> GetApproverById(int id)
        {
            return await DbContext.CompetencyApprovers
                .Include(ca => ca.User)
                .SingleOrDefaultAsync(ca => ca.CompetencyApproverId == id);
        }

        public async Task<CompetencyApprover> AddApprover(CompetencyApprover approver)
        {
            DbContext.CompetencyApprovers.Add(approver);
            await DbContext.SaveChangesAsync();
            return approver;
        }

        public async Task<bool> IsApprover(int competencyId, int userId)
        {
            return await DbContext.CompetencyApprovers
                .AsNoTracking()
                .AnyAsync(ca => ca.CompetencyId == competencyId && ca.UserId == userId && !ca.Inactive);
        }

        // Resource type competencies

        public async Task<IEnumerable<ResourceTypeCompetency>> GetResourceTypeCompetencies(int resourceTypeId)
        {
            return await DbContext.ResourceTypeCompetencies
                .Include(rtc => rtc.Competency)
                .AsNoTracking()
                .Where(rtc => rtc.ResourceTypeId == resourceTypeId)
                .ToListAsync();
        }

        public async Task<IEnumerable<ResourceTypeCompetency>> GetResourceTypeCompetenciesWithCompetency(int resourceTypeId)
        {
            return await DbContext.ResourceTypeCompetencies
                .Include(rtc => rtc.Competency)
                .AsNoTracking()
                .Where(rtc => rtc.ResourceTypeId == resourceTypeId)
                .ToListAsync();
        }

        public async Task SetResourceTypeCompetencies(int resourceTypeId, IEnumerable<ResourceTypeCompetency> competencies)
        {
            var existing = await DbContext.ResourceTypeCompetencies
                .Where(rtc => rtc.ResourceTypeId == resourceTypeId)
                .ToListAsync();

            DbContext.ResourceTypeCompetencies.RemoveRange(existing);
            DbContext.ResourceTypeCompetencies.AddRange(competencies);
            await DbContext.SaveChangesAsync();
        }
    }
}
