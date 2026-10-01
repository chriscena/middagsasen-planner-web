namespace Middagsasen.Planner.Api.Services.WorkHours
{
    public interface IWorkHoursService
    {
        Task<WorkHourResponse> CreateWorkHour(CreateWorkHourRequest request);
        Task<WorkHourResponse> UpdateWorkHour(int workHourId, UpdateWorkHourRequest request);
        Task<ApprovedByResponse> UpdateApprovedBy(int workHourId, ApprovedByRequest request);
        Task<WorkHourResponse> DeleteWorkHour(int workHourId);
        Task<PagedResponse<WorkHourResponse>> GetWorkHours(int? approved, int? page = 1, int? pageSize = 20);
        Task<PagedResponse<WorkHourResponse>> GetWorkHoursByUser(int userId, int? approved, int? page = 1, int? pageSize = 20);
        Task<WorkHourResponse> GetWorkHourById(int workHourId);
        Task<WorkHourSumResponse> GetWorkHoursSum(int? userId = null);
        Task<IEnumerable<UserWorkHourSumResponse>> GetWorkHoursSumPerUser();
    }
}
