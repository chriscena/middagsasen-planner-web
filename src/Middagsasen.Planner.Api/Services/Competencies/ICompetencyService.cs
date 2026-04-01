namespace Middagsasen.Planner.Api.Services.Competencies
{
    public interface ICompetencyService
    {
        // Competency CRUD
        Task<IEnumerable<CompetencyResponse>> GetCompetencies();
        Task<CompetencyResponse?> GetCompetencyById(int id);
        Task<CompetencyResponse> CreateCompetency(CompetencyRequest request);
        Task<CompetencyResponse?> UpdateCompetency(int id, CompetencyRequest request);
        Task<CompetencyResponse?> DeleteCompetency(int id);

        // User competencies
        Task<IEnumerable<UserCompetencyResponse>> GetUserCompetencies(int userId);
        Task<UserCompetencyResponse?> AddUserCompetency(UserCompetencyRequest request);
        Task<UserCompetencyResponse?> ApproveUserCompetency(int userCompetencyId, int approvedByUserId, ApproveCompetencyRequest request);
        Task<UserCompetencyResponse?> RevokeUserCompetency(int userCompetencyId);

        // Resource type competencies
        Task<IEnumerable<ResourceTypeCompetencyResponse>> GetResourceTypeCompetencies(int resourceTypeId);
        Task<IEnumerable<ResourceTypeCompetencyResponse>> SetResourceTypeCompetencies(int resourceTypeId, IEnumerable<SetResourceTypeCompetencyRequest> requirements);

        // Approvers
        Task<CompetencyApproverResponse?> AddApprover(int competencyId, int userId);
        Task<bool> RemoveApprover(int approverId);
    }
}
