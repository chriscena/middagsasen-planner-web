using Middagsasen.Planner.Api.Services.Events;

namespace Middagsasen.Planner.Api.Services.WorkHours
{
    public interface IWorkHoursService
    {
        Task<WorkHourResponse?> CreateWorkHour(WorkHourRequest request);//
        Task<WorkHourResponse?> UpdateWorkHourById(int workHourId, WorkHourRequest request);//
        Task<IEnumerable<WorkHourResponse>> GetWorkHours();//
        Task<PagedResponse<WorkHourResponse>> GetWorkHoursByUser(int userId, int? page, int? pageSize, int? size, int? from, int? approved);//
        Task<WorkHourResponse> GetWorkHourById(int userId);//
        Task<WorkHourResponse?> GetActiveWorkHour(int userId);//
        Task<EndTimeResponse?> UpdateEndTime(int workHourId, EndTimeRequest request);//
        Task<ApprovedByResponse?> UpdateApprovedBy(int workHourId, ApprovedByRequest request);//
        Task<WorkHourResponse?> DeleteWorkHour(int workhourId);//
    }
}