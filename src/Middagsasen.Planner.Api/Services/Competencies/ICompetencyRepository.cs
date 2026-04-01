using Middagsasen.Planner.Api.Data;

namespace Middagsasen.Planner.Api.Services.Competencies
{
    public interface ICompetencyRepository
    {
        // Competencies
        Task<IEnumerable<Competency>> GetCompetencies();
        Task<Competency?> GetCompetencyById(int id);
        Task<Competency> AddCompetency(Competency competency);
        Task SaveChangesAsync();

        // User competencies
        Task<IEnumerable<UserCompetency>> GetUserCompetencies(int userId);
        Task<UserCompetency?> GetUserCompetencyById(int id);
        Task<UserCompetency> AddUserCompetency(UserCompetency userCompetency);
        Task RemoveUserCompetency(UserCompetency userCompetency);

        // Approvers
        Task<IEnumerable<CompetencyApprover>> GetApprovers(int competencyId);
        Task<CompetencyApprover?> GetApproverById(int id);
        Task<CompetencyApprover> AddApprover(CompetencyApprover approver);
        Task<bool> IsApprover(int competencyId, int userId);

        // Resource type competencies
        Task<IEnumerable<ResourceTypeCompetency>> GetResourceTypeCompetencies(int resourceTypeId);
        Task<IEnumerable<ResourceTypeCompetency>> GetResourceTypeCompetenciesWithCompetency(int resourceTypeId);
        Task SetResourceTypeCompetencies(int resourceTypeId, IEnumerable<ResourceTypeCompetency> competencies);
    }
}
