using Middagsasen.Planner.Api.Services.Events;

namespace Middagsasen.Planner.Api.Services.WorkHours
{
    public interface IWorkHoursService
    {
        Task<WorkHourResponse?> CreateWorkHour(WorkHourRequest request);//
        Task<WorkHourResponse?> UpdateWorkHourById(int workHourId, WorkHourRequest request);//
        Task<PagedResponse<WorkHourResponse>> GetWorkHours(int? approved, int? page = 1, int? pageSize = 20);
        Task<PagedResponse<WorkHourResponse>> GetWorkHoursByUser(int userId, int? approved, int? page = 1, int? pageSize = 20);//
        Task<WorkHourResponse?> GetWorkHourById(int userId);//
        Task<WorkHourResponse?> GetActiveWorkHour(int userId);//
        Task<EndTimeResponse?> UpdateEndTime(int workHourId, EndTimeRequest request);//
        Task<ApprovedByResponse?> UpdateApprovedBy(int workHourId, ApprovedByRequest request);//
        Task<WorkHourResponse?> DeleteWorkHour(int workhourId);//
        Task<WorkHourSumResponse> GetWorkHoursSum(int? userId = null);
    }
}