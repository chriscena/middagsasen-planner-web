﻿using Microsoft.EntityFrameworkCore;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Core;
using Middagsasen.Planner.Api.Services.ResourceTypes;
using Middagsasen.Planner.Api.Services.SmsSender;
using Middagsasen.Planner.Api.Services.Storage;
using Middagsasen.Planner.Api.Services.Events;
using Azure.Core;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using Middagsasen.Planner.Api.Services.Users;
using Azure.Storage.Blobs.Models;
using Microsoft.IdentityModel.Tokens;
using System.Security.AccessControl;

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

        public async Task<IEnumerable<WorkHourResponse>> GetWorkHours()
        {
            var workHours = await DbContext.WorkHours
               .AsNoTracking()
               .ToListAsync();

            return DbContext.WorkHours.Select(Map).ToList();

        }

        public async Task<WorkHourResponse?> GetWorkHourById(int id)
        {
            var existingWorkHour = await DbContext.WorkHours
                .AsNoTracking()
                .SingleOrDefaultAsync(w => w.WorkHourId == id);

            return (existingWorkHour == null) ? null : Map(existingWorkHour);
        }

        public async Task<PagedResponse<WorkHourResponse>> GetWorkHoursByUser(int userId, int? page, int? pageSize, int? size, int? from, int? approved)
        {
            var tableType = 0;//1 = pagination, 2 = vScroll
            var skip = 0;
            var someSize = 0;

            if (!(page ==null ) || !(pageSize == null)) {
                skip = (int)(page > 0 ? (page - 1) * pageSize : 0);
                someSize = (int)pageSize;
                tableType = 1;
            }
            else if (!(size == null) || !(from == null)) {
                someSize = (int)size;
                tableType = 2;
            }

            var query = DbContext.WorkHours
                .AsNoTracking()
                .Where(w => w.UserId == userId);

            var approvedFilter = approved;

            if (approvedFilter == 1)
                query = query.Where(w => w.ApprovalStatus == 1);
            if (approvedFilter == 2)
                query = query.Where(w => w.ApprovalStatus == 2);
            if (approvedFilter == 3)
                query = query.Where(w => !w.ApprovalStatus.HasValue);

            var totalCount = query.Count();

            List<WorkHour> existingWorkHours;

            if (tableType == 1)
            {
                 existingWorkHours = await query
                .OrderByDescending(w => w.StartTime)
                .Skip(skip)
                .Take(someSize)
                .ToListAsync();
            }
            else if (tableType == 2)
            {
                existingWorkHours = await query
                .OrderByDescending(w => w.StartTime)
                .Take(someSize)
                .ToListAsync();
            }
            else
            {
                existingWorkHours = new List<WorkHour>(); // fallback
            }





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
            double interval = 0;
            if (workHour.EndTime.HasValue && workHour.StartTime.HasValue)
            {
               interval = (workHour.EndTime.Value - workHour.StartTime.Value).TotalHours;
            }

            return new WorkHourResponse
            {
                WorkHourId = workHour.WorkHourId,
                UserId = workHour.UserId,
                StartTime = workHour.StartTime,
                EndTime = workHour.EndTime,
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
