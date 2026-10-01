using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Core;
using Middagsasen.Planner.Api.Data;

namespace Middagsasen.Planner.Api.Services.WorkHours
{
    public class WorkHoursService : IWorkHoursService
    {
        internal const string ForbiddenMessage = "Du har ikke tilgang til å utføre denne handlingen.";
        internal const string LockedMessage = "Timeføringen er allerede behandlet og kan ikke endres.";
        internal const string NoStatusToResetMessage = "Timeføringen har ingen status som kan fjernes.";
        internal const string NotFoundMessage = "Timeføringen finnes ikke.";

        public WorkHoursService(IWorkHourRepository repository, ICurrentUserService currentUser)
        {
            Repository = repository;
            CurrentUser = currentUser;
        }

        public IWorkHourRepository Repository { get; }
        public ICurrentUserService CurrentUser { get; }

        public async Task<WorkHourResponse> CreateWorkHour(CreateWorkHourRequest request)
        {
            if (!request.StartTime.HasValue)
                throw new InvalidOperationException("Starttid må oppgis.");

            var workHour = new WorkHour
            {
                UserId = CurrentUser.UserId,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                Description = request.Description,
            };
            Repository.Add(workHour);
            await Repository.SaveChangesAsync();

            return await GetMapped(workHour.WorkHourId);
        }

        public async Task<WorkHourResponse> UpdateWorkHour(int workHourId, UpdateWorkHourRequest request)
        {
            var workHour = await GetTracked(workHourId);
            var userId = CurrentUser.UserId;
            var isAdmin = CurrentUser.IsAdmin;

            var hasContent = request.StartTime.HasValue || request.EndTime.HasValue || request.Description != null;
            var hasStatus = request.ApprovalStatus.HasValue;

            if (hasStatus)
                ValidateStatus(request.ApprovalStatus);

            if (!hasContent && !hasStatus)
            {
                if (!WorkHourPolicy.CanRead(workHour, isAdmin, userId))
                    throw new ForbiddenAccessException(ForbiddenMessage);
                return Map(workHour);
            }

            // Alle tilgangssjekker gjøres mot tilstanden FØR endring.
            if (hasContent)
                Ensure(WorkHourPolicy.CanEdit(workHour, isAdmin, userId), LockedMessage);
            if (hasStatus)
                Ensure(WorkHourPolicy.CanSetStatus(workHour, isAdmin, userId, request.ApprovalStatus), LockedMessage);

            var contentChanged = false;
            if (request.StartTime.HasValue && request.StartTime != workHour.StartTime)
            {
                workHour.StartTime = request.StartTime;
                contentChanged = true;
            }
            if (request.EndTime.HasValue && request.EndTime != workHour.EndTime)
            {
                workHour.EndTime = request.EndTime;
                contentChanged = true;
            }
            if (request.Description != null && request.Description != workHour.Description)
            {
                workHour.Description = request.Description;
                contentChanged = true;
            }

            // ModifiedBy settes kun ved faktisk innholdsendring utført av en annen enn eier,
            // og nullstilles aldri når eier redigerer senere.
            if (contentChanged && !WorkHourPolicy.IsOwner(workHour, userId))
            {
                workHour.ModifiedBy = userId;
                workHour.ModifiedTime = DateTime.UtcNow;
            }

            if (hasStatus)
                ApplyStatus(workHour, request.ApprovalStatus, userId);

            // Innhold og status lagres i én operasjon.
            await Repository.SaveChangesAsync();

            return await GetMapped(workHourId);
        }

        public async Task<ApprovedByResponse> UpdateApprovedBy(int workHourId, ApprovedByRequest request)
        {
            var workHour = await GetTracked(workHourId);
            var userId = CurrentUser.UserId;

            ValidateStatus(request.ApprovalStatus);
            Ensure(
                WorkHourPolicy.CanSetStatus(workHour, CurrentUser.IsAdmin, userId, request.ApprovalStatus),
                request.ApprovalStatus.HasValue ? LockedMessage : NoStatusToResetMessage);

            ApplyStatus(workHour, request.ApprovalStatus, userId);
            await Repository.SaveChangesAsync();

            return new ApprovedByResponse
            {
                WorkHourId = workHour.WorkHourId,
                ApprovedBy = workHour.ApprovedBy,
                ApprovalStatus = workHour.ApprovalStatus,
                ApprovedTime = workHour.ApprovedTime.AsUtc(),
            };
        }

        public async Task<WorkHourResponse> DeleteWorkHour(int workHourId)
        {
            var workHour = await GetTracked(workHourId);
            Ensure(WorkHourPolicy.CanEdit(workHour, CurrentUser.IsAdmin, CurrentUser.UserId), LockedMessage);

            var response = Map(workHour);
            Repository.Remove(workHour);
            await Repository.SaveChangesAsync();
            return response;
        }

        public async Task<PagedResponse<WorkHourResponse>> GetWorkHours(int? approved, int? page = 1, int? pageSize = 20)
        {
            EnsureAdmin();
            return await GetPaged(null, approved, page, pageSize);
        }

        public async Task<PagedResponse<WorkHourResponse>> GetWorkHoursByUser(int userId, int? approved, int? page = 1, int? pageSize = 20)
        {
            EnsureAdminOrSelf(userId);
            return await GetPaged(userId, approved, page, pageSize);
        }

        public async Task<WorkHourResponse> GetWorkHourById(int workHourId)
        {
            var workHour = await Repository.GetWorkHourByIdReadOnly(workHourId)
                ?? throw new EntityNotFoundException(NotFoundMessage);

            if (!WorkHourPolicy.CanRead(workHour, CurrentUser.IsAdmin, CurrentUser.UserId))
                throw new ForbiddenAccessException(ForbiddenMessage);

            return Map(workHour);
        }

        public async Task<WorkHourSumResponse> GetWorkHoursSum(int? userId = null)
        {
            if (userId.HasValue)
                EnsureAdminOrSelf(userId.Value);
            else
                EnsureAdmin();

            var byStatus = SumByStatus(await Repository.GetIntervals(userId));

            return new WorkHourSumResponse
            {
                PendingHours = byStatus.Pending,
                ApprovedHours = byStatus.Approved,
                RejectedHours = byStatus.Rejected,
            };
        }

        public async Task<IEnumerable<UserWorkHourSumResponse>> GetWorkHoursSumPerUser()
        {
            EnsureAdmin();

            var now = DateTime.UtcNow;
            var m = DateTimeExtensions.SeasonStartMonth;
            var seasonStart = now.Month < m
                ? new DateTime(now.Year - 1, m, 1)
                : new DateTime(now.Year, m, 1);

            var intervals = await Repository.GetIntervals(null, seasonStart);

            return intervals
                .GroupBy(h => h.UserId)
                .Select(g =>
                {
                    var byStatus = SumByStatus(g);
                    return new UserWorkHourSumResponse
                    {
                        UserId = g.Key,
                        PendingHours = byStatus.Pending,
                        ApprovedHours = byStatus.Approved,
                        RejectedHours = byStatus.Rejected,
                    };
                })
                .ToList();
        }

        private async Task<PagedResponse<WorkHourResponse>> GetPaged(int? userId, int? approved, int? page, int? pageSize)
        {
            var take = pageSize ?? 20;
            var pageToUse = page.HasValue && page.Value > 0 ? page.Value : 1;
            var skip = (pageToUse - 1) * take;

            var (items, totalCount) = await Repository.GetWorkHours(userId, approved, skip, take);
            return new PagedResponse<WorkHourResponse> { Result = items.Select(Map).ToList(), TotalCount = totalCount };
        }

        private async Task<WorkHour> GetTracked(int workHourId)
        {
            return await Repository.GetWorkHourById(workHourId)
                ?? throw new EntityNotFoundException(NotFoundMessage);
        }

        private async Task<WorkHourResponse> GetMapped(int workHourId)
        {
            var workHour = await Repository.GetWorkHourByIdReadOnly(workHourId)
                ?? throw new EntityNotFoundException(NotFoundMessage);
            return Map(workHour);
        }

        private static void ValidateStatus(int? status)
        {
            if (status.HasValue && status is not (WorkHourPolicy.Approved or WorkHourPolicy.Rejected))
                throw new InvalidOperationException("Ugyldig status. Gyldige verdier er 1 (godkjent) og 2 (avslått).");
        }

        private static void ApplyStatus(WorkHour workHour, int? status, int userId)
        {
            workHour.ApprovalStatus = status;
            if (status.HasValue)
            {
                workHour.ApprovedBy = userId;
                workHour.ApprovedTime = DateTime.UtcNow;
            }
            else
            {
                workHour.ApprovedBy = null;
                workHour.ApprovedTime = null;
            }
        }

        private static void Ensure(WorkHourAccess access, string lockedMessage)
        {
            switch (access)
            {
                case WorkHourAccess.Forbidden:
                    throw new ForbiddenAccessException(ForbiddenMessage);
                case WorkHourAccess.Locked:
                    throw new EntityLockedException(lockedMessage);
            }
        }

        private void EnsureAdmin()
        {
            if (!CurrentUser.IsAdmin)
                throw new ForbiddenAccessException(ForbiddenMessage);
        }

        private void EnsureAdminOrSelf(int userId)
        {
            if (!CurrentUser.IsAdmin && CurrentUser.UserId != userId)
                throw new ForbiddenAccessException(ForbiddenMessage);
        }

        private static (double Pending, double Approved, double Rejected) SumByStatus(IEnumerable<WorkHourInterval> intervals)
        {
            var byStatus = intervals
                .GroupBy(h => h.ApprovalStatus ?? 0)
                .ToDictionary(g => g.Key, g => g.Sum(h => h.Hours));

            static double Get(Dictionary<int, double> d, int key) => d.TryGetValue(key, out var v) ? Math.Round(v, 1) : 0;

            return (Get(byStatus, 0), Get(byStatus, WorkHourPolicy.Approved), Get(byStatus, WorkHourPolicy.Rejected));
        }

        private static string? MapFullName(User? user)
        {
            if (user == null) return null;
            return $"{user.FirstName ?? ""} {user.LastName ?? ""}".Trim();
        }

        private static WorkHourResponse Map(WorkHour workHour)
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
                ApprovedByName = MapFullName(workHour.ApprovedByUser),
                ApprovedTime = workHour.ApprovedTime.AsUtc(),
                ApprovalStatus = workHour.ApprovalStatus,
                ModifiedBy = workHour.ModifiedBy,
                ModifiedByName = MapFullName(workHour.ModifiedByUser),
                ModifiedTime = workHour.ModifiedTime.AsUtc(),
            };
        }
    }
}
