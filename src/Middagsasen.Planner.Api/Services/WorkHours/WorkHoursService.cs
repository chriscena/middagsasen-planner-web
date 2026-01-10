using Microsoft.EntityFrameworkCore;
using Middagsasen.Planner.Api.Core;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services.SmsSender;
using Middagsasen.Planner.Api.Services.Storage;

namespace Middagsasen.Planner.Api.Services.WorkHours
{
    public class WorkHoursService : IWorkHoursService
    {
        public WorkHoursService(PlannerDbContext dbContext, ISmsSender smsSender, IStorageService storage)
        {
            DbContext = dbContext;
            SmsSender = smsSender;
            Storage = storage;
        }

        public PlannerDbContext DbContext { get; }
        public ISmsSender SmsSender { get; }
        public IStorageService Storage { get; }

        public async Task<WorkHourResponse?> CreateWorkHour(WorkHourRequest request)
        {
            var newWorkHour = new WorkHour
            {
                UserId = request.UserId,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                Description = request.Description
            };
            DbContext.WorkHours.Add(newWorkHour);
            await DbContext.SaveChangesAsync();

            return await GetWorkHourById(newWorkHour.WorkHourId);
        }

        public async Task<PagedResponse<WorkHourResponse>> GetWorkHours(int? approved, int? page = 1, int? pageSize = 20)
        {
            var take = pageSize ?? 20;
            var pageToUse = page.HasValue && page.Value > 0 ? page.Value : 1;
            var skip = (pageToUse - 1) * take;

            var query = DbContext.WorkHours
               .AsNoTracking();

            if (approved == 1)
                query = query.Where(w => w.ApprovalStatus == 1);
            if (approved == 2)
                query = query.Where(w => w.ApprovalStatus == 2);
            if (approved == 3)
                query = query.Where(w => !w.ApprovalStatus.HasValue);

            var totalCount = query.Count();
            var existingWorkHours = await query
                .OrderByDescending(w => w.StartTime)
                .Skip(skip)
                .Take(take)
                .ToListAsync();

            var result = existingWorkHours.Select(Map).ToList();

            return new PagedResponse<WorkHourResponse> { Result = result, TotalCount = totalCount };
        }

        public async Task<WorkHourSumResponse> GetWorkHoursSum(int? userId = null)
        {
            var query = DbContext.WorkHours
               .AsNoTracking();

            if (userId.HasValue)
                query = query.Where(w => w.UserId == userId.Value);

            var existingWorkHours = (await query
                .Where(w => w.EndTime.HasValue && w.StartTime.HasValue)
                .Select(h => new { Status = h.ApprovalStatus ?? 0, Hours = (h.EndTime!.Value - h.StartTime!.Value).TotalHours })
                .ToListAsync())
                .GroupBy(h => h.Status)
                .Select(h => new { Status = h.Key, TotalHours = h.Sum(t => t.Hours)})
                .ToDictionary(h => h.Status, h => h.TotalHours);

            return new WorkHourSumResponse
            {
                PendingHours = existingWorkHours.TryGetValue(0, out double value) ? Math.Round(value, 1) : 0,
                ApprovedHours = existingWorkHours.TryGetValue(1, out double value1) ? Math.Round(value1, 1) : 0,
                RejectedHours = existingWorkHours.TryGetValue(2, out double value2) ? Math.Round(value2, 1) : 0,
            };
        }

        public async Task<WorkHourResponse?> GetWorkHourById(int id)
        {
            var existingWorkHour = await DbContext.WorkHours
                .AsNoTracking()
                .SingleOrDefaultAsync(w => w.WorkHourId == id);

            return (existingWorkHour == null) ? null : Map(existingWorkHour);
        }

        public async Task<PagedResponse<WorkHourResponse>> GetWorkHoursByUser(int userId, int? approved, int? page = 1, int? pageSize = 20)
        {
            var take = pageSize ?? 20;
            var pageToUse = page.HasValue && page.Value > 0 ? page.Value : 1;
            var skip = (pageToUse - 1) * take;

            var query = DbContext.WorkHours
                .AsNoTracking()
                .Where(w => w.UserId == userId);

            if (approved == 1)
                query = query.Where(w => w.ApprovalStatus == 1);
            if (approved == 2)
                query = query.Where(w => w.ApprovalStatus == 2);
            if (approved == 3)
                query = query.Where(w => !w.ApprovalStatus.HasValue);

            var totalCount = query.Count();

            List<WorkHour> existingWorkHours;

            existingWorkHours = await query
                .OrderByDescending(w => w.StartTime)
                .Skip(skip)
                .Take(take)
                .ToListAsync();

            var result = existingWorkHours.Select(Map).ToList();

            return new PagedResponse<WorkHourResponse> { Result = result, TotalCount = totalCount };
        }

        public async Task<WorkHourResponse?> GetActiveWorkHour(int userId)
        {
            var query = DbContext.WorkHours
               .AsNoTracking()
               .Where(w => w.EndTime == null && w.UserId == userId);

            var existingWorkHour = await query.FirstOrDefaultAsync();

            return (existingWorkHour == null) ? null : Map(existingWorkHour);
        }

        private WorkHourResponse Map(WorkHour workHour)
        {
            decimal interval = 0;
            if (workHour.EndTime.HasValue && workHour.StartTime.HasValue)
            {
                interval = (decimal)Math.Round((workHour.EndTime.Value - workHour.StartTime.Value).TotalHours, 1);
            }

            return new WorkHourResponse
            {
                WorkHourId = workHour.WorkHourId,
                UserId = workHour.UserId,
                StartTime = workHour.StartTime.AsUtc(),
                EndTime = workHour.EndTime.AsUtc(),
                Hours = interval,
                Description = workHour.Description,
                ApprovedBy = workHour.ApprovedBy,
                ApprovalStatus = workHour.ApprovalStatus
            };
        }

        public async Task<WorkHourResponse?> UpdateWorkHourById(int workHourId, WorkHourRequest request)
        {
            var workHour = await DbContext.WorkHours.SingleOrDefaultAsync(w => w.WorkHourId == workHourId);
            if (workHour == null) return null;

            if (request.StartTime.HasValue)
                workHour.StartTime = request.StartTime;
            if (request.EndTime.HasValue)
                workHour.EndTime = request.EndTime.Value;
            if (!string.IsNullOrWhiteSpace(request.Description))
                workHour.Description = request.Description;
            if (request.ApprovedBy.HasValue)
                workHour.ApprovedBy = request.ApprovedBy;
            if (request.ApprovalStatus.HasValue)
                workHour.ApprovalStatus = request.ApprovalStatus;

            await DbContext.SaveChangesAsync();

            return Map(workHour);
        }

        public async Task<EndTimeResponse?> UpdateEndTime(int workHourId, EndTimeRequest request)
        {
            var workHour = await DbContext.WorkHours
                .SingleOrDefaultAsync(w => w.WorkHourId == workHourId);

            if (workHour == null) return null;

            workHour.EndTime = request.EndTime;
            await DbContext.SaveChangesAsync();

            return new EndTimeResponse
            {
                WorkHourId = workHour.WorkHourId,
                EndTime = workHour.EndTime
            };
        }

        public async Task<ApprovedByResponse?> UpdateApprovedBy(int workHourId, ApprovedByRequest request)
        {
            var workHour = await DbContext.WorkHours
                .SingleOrDefaultAsync(w => w.WorkHourId == workHourId);

            if (workHour == null) return null;

            workHour.ApprovedBy = request.ApprovedBy;
            workHour.ApprovalStatus = request.ApprovalStatus;
            workHour.ApprovedTime = DateTime.Now;
            await DbContext.SaveChangesAsync();

            return new ApprovedByResponse
            {
                WorkHourId = workHour.WorkHourId,
                ApprovedBy = workHour.ApprovedBy,
                ApprovalStatus = workHour.ApprovalStatus,
                ApprovedTime = workHour.ApprovedTime,
            };
        }

        public async Task<WorkHourResponse?> DeleteWorkHour(int workHourId)
        {
            var workHour = await DbContext.WorkHours.SingleOrDefaultAsync(w => w.WorkHourId == workHourId);
            if (workHour == null) return null;

            DbContext.WorkHours.Remove(workHour);
            await DbContext.SaveChangesAsync();

            return Map(workHour);
        }
    }
}
