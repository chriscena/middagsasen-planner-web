namespace Middagsasen.Planner.Api.Services.WorkHours
{
    public interface IWorkHoursService
    {
        Task<WorkHourResponse> CreateWorkHour(CreateWorkHourRequest request);
        Task<WorkHourResponse> UpdateWorkHour(int workHourId, UpdateWorkHourRequest request);
        Task<ApprovedByResponse> UpdateApprovedBy(int workHourId, ApprovedByRequest request);
        Task<WorkHourResponse> DeleteWorkHour(int workHourId);
        /// <summary>Føringer for alle (eller én) brukere, kun admin. <paramref name="season"/> = sesongens startår; null = alle sesonger.</summary>
        Task<PagedResponse<WorkHourResponse>> GetWorkHours(int? userId, int? approved, int? season, int? page = 1, int? pageSize = 20);
        /// <summary>Føringer for én bruker. <paramref name="season"/> = sesongens startår; null = alle sesonger.</summary>
        Task<PagedResponse<WorkHourResponse>> GetWorkHoursByUser(int userId, int? approved, int? season, int? page = 1, int? pageSize = 20);
        Task<WorkHourResponse> GetWorkHourById(int workHourId);
        /// <summary>Sum timer per status. <paramref name="season"/> = sesongens startår; null = alle sesonger.</summary>
        Task<WorkHourSumResponse> GetWorkHoursSum(int? userId = null, int? season = null);
        Task<IEnumerable<UserWorkHourSumResponse>> GetWorkHoursSumPerUser();
    }
}
