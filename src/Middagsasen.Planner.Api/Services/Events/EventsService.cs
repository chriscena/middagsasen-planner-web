using Microsoft.EntityFrameworkCore;
using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Core;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services.ResourceTypes;

namespace Middagsasen.Planner.Api.Services.Events
{
    public class EventsService : IEventsService
    {
        public EventsService(PlannerDbContext dbContext, IResourceTypesService resourceTypesService, ICurrentUserService currentUser)
        {
            DbContext = dbContext;
            ResourceTypesService = resourceTypesService;
            CurrentUser = currentUser;
        }

        public PlannerDbContext DbContext { get; }
        public IResourceTypesService ResourceTypesService { get; }
        public ICurrentUserService CurrentUser { get; }

        public async Task<IEnumerable<EventStatusResponse>> GetEventStatuses(int month, int year)
        {
            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1);
            var eventStatuses = await DbContext.EventStatuses.Where(e => e.StartTime >= startDate && e.StartTime < endDate).ToListAsync();

            var response = eventStatuses.Select(e => new
            {
                Date = e.StartTime.ToString("yyyy'/'MM'/'dd"),
                IsMissingStaff = e.MissingStaff == 1,
            })
                .GroupBy(r => r.Date)
                .Select(g => new EventStatusResponse
                {
                    Date = g.Key,
                    IsMissingStaff = g.Any(e => e.IsMissingStaff),
                });
            return response;
        }

        private IQueryable<Event> Events => DbContext.Events
                .Include(e => e.Resources)
                    .ThenInclude(r => r.Shifts)
                        .ThenInclude(s => s.User)
                            .ThenInclude(u =>u.Trainings)
                .Include(e => e.Resources)
                    .ThenInclude(r => r.Shifts)
                        .ThenInclude(s => s.User)
                            .ThenInclude(u => u.Competencies)
                                .ThenInclude(uc => uc.Competency)
                .Include(e => e.Resources)
                    .ThenInclude(r => r.ResourceType)
                        .ThenInclude(rt => rt.Trainers)
                            .ThenInclude(t => t.User)
                .Include(e => e.Resources)
                    .ThenInclude(r => r.ResourceType)
                        .ThenInclude(rt => rt.Files)
                .Include(e => e.Resources)
                    .ThenInclude(r => r.ResourceType)
                        .ThenInclude(rt => rt.RequiredCompetencies)
                            .ThenInclude(rc => rc.Competency)
                .Include(e => e.Resources)
                    .ThenInclude(r => r.Messages)
                        .ThenInclude(t => t.CreatedByUser);

        public async Task<IEnumerable<EventResponse?>> GetEvents()
        {
            var events = await Events
                .AsNoTracking()
                .ToListAsync();

            return events.Select(Map).ToList();
        }

        public async Task<IEnumerable<EventResponse?>> GetEvents(DateTime start, DateTime end)
        {
            var events = await Events
                .AsNoTracking()
                .Where(e => e.StartTime >= start && e.StartTime < end)
                .AsSplitQuery()
                .ToListAsync();

            return events.Select(Map).ToList();
        }

        public async Task<EventResponse> GetEventById(int id)
        {
            var existingEvent = await Events
                .AsNoTracking()
                .SingleOrDefaultAsync(e => e.EventId == id)
                ?? throw new EntityNotFoundException("Kunne ikke finne vakt.");

            return Map(existingEvent);
        }

        public async Task<IEnumerable<ShiftSeasonResponse>> GetShiftsByUserId(int id)
        {
            var shifts = await DbContext.Shifts
                .Include(e => e.Resource)
                    .ThenInclude(e => e.ResourceType)
                .AsNoTracking()
                .Where(s => s.UserId == id)
                .ToListAsync();

            var response = shifts
                .Select(s => new UserShiftResponse
                {
                    Id = s.EventResourceUserId,
                    StartDate = s.StartTime?.ToString("yyyy'-'MM'-'dd"),
                    StartTime = s.StartTime.ToSimpleIsoString(),
                    EndTime = s.EndTime.ToSimpleIsoString(),
                    ResourceName = s.Resource?.ResourceType?.Name,
                    Season = s.StartTime.ToSeason(),
                    Comment = s.Comment,
                })
                .OrderByDescending(s => s.StartTime)
                .GroupBy(s => s.Season)
                .Select(s => new ShiftSeasonResponse { Label = s.Key, Shifts = [..s] })
                .ToList();

            return response;
        }

        public async Task<EventResponse> CreateEvent(EventRequest request)
        {
            var newEvent = new Event
            {
                Name = request.Name,
                Description = request.Description,
                StartTime = DateTime.Parse(request.StartTime),
                EndTime = DateTime.Parse(request.EndTime),
                Resources = request.Resources.Select(Map).ToList(),
            };

            DbContext.Events.Add(newEvent);
            await DbContext.SaveChangesAsync();

            return await GetEventById(newEvent.EventId);
        }

        public async Task<EventResponse?> UpdateEvent(int eventId, EventRequest request)
        {
            var existingEvent = await Events
            .SingleOrDefaultAsync(e => e.EventId == eventId);

            if (existingEvent == null) return null;

            existingEvent.Name = request.Name;
            existingEvent.Description = request.Description;
            existingEvent.StartTime = DateTime.Parse(request.StartTime);
            existingEvent.EndTime = DateTime.Parse(request.EndTime);

            foreach (var resource in request.Resources)
            {
                if (resource.IsDeleted)
                {
                    var resourceToDelete = existingEvent.Resources.FirstOrDefault(r => r.EventResourceId == resource.Id);
                    if (resourceToDelete == null) continue;
                    existingEvent.Resources.Remove(resourceToDelete);
                }
                else if (!resource.Id.HasValue)
                {
                    existingEvent.Resources.Add(Map(resource));
                }
                else
                {
                    var resourceToUpdate = existingEvent.Resources.FirstOrDefault(r => r.EventResourceId == resource.Id);
                    if (resourceToUpdate == null) continue;
                    resourceToUpdate.ResourceTypeId = resource.ResourceTypeId;
                    resourceToUpdate.StartTime = DateTime.Parse(resource.StartTime);
                    resourceToUpdate.EndTime = DateTime.Parse(resource.EndTime);
                    resourceToUpdate.MinimumStaff = resource.MinimumStaff;
                }
            }
            await DbContext.SaveChangesAsync();

            var response = await Events
            .SingleOrDefaultAsync(e => e.EventId == eventId);

            return response != null ? Map(response) : null;
        }

        public async Task<EventResponse?> DeleteEvent(int id)
        {
            var existingEvent = await DbContext.Events.SingleOrDefaultAsync(e => e.EventId == id);

            if (existingEvent == null) return null;

            DbContext.Events.Remove(existingEvent);

            await DbContext.SaveChangesAsync();
            return Map(existingEvent);
        }

        public async Task<ShiftResponse?> AddShift(int eventResourceId, ShiftRequest request)
        {
            if (!CurrentUser.IsAdmin && request.UserId != CurrentUser.UserId)
                throw new ForbiddenAccessException();

            if (request.Training != null)
            {
                request.Training.ConfirmedBy = CurrentUser.UserId;
            }

            var newShift = new EventResourceUser
            {
                EventResourceId = eventResourceId,
                UserId = request.UserId,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                Comment = request.Comment,
            };
            DbContext.Shifts.Add(newShift);

            await DbContext.SaveChangesAsync();

            if (request.Training != null)
            {
                if (request.Training.Id == 0)
                    await ResourceTypesService.CreateTraining(request.Training.ResourceTypeId, request.Training);
                else
                    await ResourceTypesService.UpdateTraining(request.Training.ResourceTypeId, request.Training);
            }

            var responseShift = await DbContext.Shifts
                .Include(s => s.Resource)
                .Include(s => s.User)
                    .ThenInclude(u => u.Trainings)
                .AsNoTracking()
                .SingleOrDefaultAsync(s => s.EventResourceUserId == newShift.EventResourceUserId);
            return responseShift == null ? null : Map(responseShift);
        }

        public async Task<ShiftResponse?> UpdateShift(int id, ShiftRequest request)
        {
            if (request.UserId == 0) request.UserId = CurrentUser.UserId;

            if (!CurrentUser.IsAdmin && request.UserId != CurrentUser.UserId)
                throw new ForbiddenAccessException();

            if (request.Training != null)
            {
                request.Training.ConfirmedBy = CurrentUser.UserId;
            }

            var shift = await DbContext.Shifts.Include(s => s.User).SingleOrDefaultAsync(s => s.EventResourceUserId == id);
            if (shift == null) return null;

            if (request.StartTime.HasValue)
                shift.StartTime = request.StartTime;

            if (request.EndTime.HasValue)
                shift.EndTime = request.EndTime;

            shift.UserId = request.UserId;
            shift.Comment = request.Comment;

            await DbContext.SaveChangesAsync();



            if (request.Training != null)
            {
                if (request.Training.Id == 0)
                    await ResourceTypesService.CreateTraining(request.Training.ResourceTypeId, request.Training);
                else
                    await ResourceTypesService.UpdateTraining(request.Training.ResourceTypeId, request.Training);
            }

            var responseShift = await DbContext.Shifts
                .Include(s => s.Resource)
                .Include(s => s.User)
                        .ThenInclude(u => u.Trainings)
                .AsNoTracking()
                .SingleOrDefaultAsync(s => s.EventResourceUserId == shift.EventResourceUserId);
            return responseShift == null ? null : Map(responseShift);
        }

        public async Task<ShiftResponse?> DeleteShift(int id)
        {
            var shift = await DbContext.Shifts.Include(s => s.User).SingleOrDefaultAsync(s => s.EventResourceUserId == id);
            if (shift == null) return null;
            if (!CurrentUser.IsAdmin && shift.UserId != CurrentUser.UserId)
                throw new ForbiddenAccessException();

            DbContext.Shifts.Remove(shift);
            await DbContext.SaveChangesAsync();

            return Map(shift);
        }

        public async Task<EventResponse?> CreateEventFromTemplate(int templateId, EventFromTemplateRequest request)
        {
            var startDate = DateTime.Parse(request.StartDate);

            var template = await DbContext.EventTemplates
                .Include(e => e.ResourceTemplates)
                .AsNoTracking()
                .SingleOrDefaultAsync(e => e.EventTemplateId == templateId);

            if (template == null) return null;

            var startTime = startDate.Date + template.StartTime.TimeOfDay;
            var endTime = startDate.Date + template.EndTime.TimeOfDay;
            if (startTime > endTime) endTime = endTime.AddDays(1);

            var newEvent = new Event
            {
                Name = template.EventName,
                StartTime = startTime,
                EndTime = endTime,
                Resources = template.ResourceTemplates.Select(r =>
                {
                    var resourceStartTime = startDate.Date + r.StartTime.TimeOfDay;
                    var resourceEndTime = startDate.Date + r.EndTime.TimeOfDay;
                    if (resourceStartTime > resourceEndTime) resourceEndTime = resourceEndTime.AddDays(1);
                    return new EventResource
                    {
                        ResourceTypeId = r.ResourceTypeId,
                        StartTime = resourceStartTime,
                        EndTime = resourceEndTime,
                        MinimumStaff = r.MinimumStaff,
                    };
                }).ToList(),
            };

            DbContext.Events.Add(newEvent);
            await DbContext.SaveChangesAsync();

            return await GetEventById(newEvent.EventId);
        }

        private TrainingResponse Map(ResourceTypeTraining training) => new TrainingResponse
        {
            Id = training.ResourceTypeTrainingId,
            ResourceTypeId = training.ResourceTypeId,
            ResourceTypeName = training.ResourceType?.Name,
            TrainingComplete = training.TrainingComplete,
            Confirmed = training.Confirmed?.ToSimpleIsoString(),
            ConfirmedById = training.ConfirmedBy,
            ConfirmedByName = MapFullName(training.ConfirmedByUser?.FirstName, training.ConfirmedByUser?.LastName),
        };

        private ResourceTypeResponse Map(ResourceType resourceType) => new ResourceTypeResponse
        {
            Id = resourceType.ResourceTypeId,
            Name = resourceType.Name,
            DefaultStaff = resourceType.DefaultStaff,
            NotificationMessage = resourceType.NotificationMessage,
            HasTraining = resourceType.Trainers.Any(),
            Trainers = resourceType.Trainers.Select(Map).ToList(),
            Files = resourceType.Files.Select(Map).ToList(),
        };
            
        private ResourceTypeTrainerResponse Map(ResourceTypeTrainer resourceTypeTrainer) => new ResourceTypeTrainerResponse
        {
            Id = resourceTypeTrainer.ResourceTypeTrainerId,
            UserId = resourceTypeTrainer.UserId,
            FullName = MapFullName(resourceTypeTrainer.User.FirstName, resourceTypeTrainer.User.LastName),
            PhoneNo = resourceTypeTrainer.User.UserName,
        };

        private FileInfoResponse Map(ResourceTypeFile file) => new FileInfoResponse
        {
            Id = file.ResourceTypeFileId,
            ResourceTypeId = file.ResourceTypeId,
            FileName = file.FileName,
            Description = file.Description,
            MimeType = file.MimeType,
            Created = file.Created.AsUtc().ToIsoString(),
            CreatedBy = MapFullName(file.CreatedByUser?.FirstName, file.CreatedByUser?.LastName),
            Updated = file.Updated.AsUtc().ToIsoString(),
            UpdatedBy = MapFullName(file.UpdatedByUser?.FirstName, file.UpdatedByUser?.LastName),
        };

        private string MapFullName(string? firstName, string? lastName)
        {
            return $"{firstName ?? ""} {lastName ?? ""}".Trim();
        }

        private EventResponse Map(Event evnt) => new EventResponse
        {
            Id = evnt.EventId,
            Name = evnt.Name,
            Description = evnt.Description,
            StartTime = evnt.StartTime.ToSimpleIsoString(),
            EndTime = evnt.EndTime.ToSimpleIsoString(),
            Resources = evnt.Resources.Select(Map).OrderBy(r => r.ResourceType.Id).ThenBy(r => r.StartTime).ToList()
        };

        private ResourceResponse Map(EventResource resource) => new ResourceResponse
        {
            Id = resource.EventResourceId,
            EventId = resource.EventId,
            ResourceType = Map(resource.ResourceType),
            StartTime = resource.StartTime.ToSimpleIsoString(),
            EndTime = resource.EndTime.ToSimpleIsoString(),
            MinimumStaff = resource.MinimumStaff,
            Shifts = resource.Shifts.Select(Map).ToList(),
            Messages = resource.Messages.Select(Map).ToList(),
            CompetencyWarnings = GetCompetencyWarnings(resource),
        };

        private IEnumerable<CompetencyWarningResponse>? GetCompetencyWarnings(EventResource resource)
        {
            var requiredCompetencies = resource.ResourceType?.RequiredCompetencies;
            if (requiredCompetencies == null || !requiredCompetencies.Any())
                return null;

            var now = DateTime.UtcNow;
            var warnings = new List<CompetencyWarningResponse>();

            foreach (var rc in requiredCompetencies)
            {
                if (rc.Competency == null) continue;

                var count = resource.Shifts
                    .Count(s => s.User?.Competencies != null &&
                        s.User.Competencies.Any(uc =>
                            uc.CompetencyId == rc.CompetencyId
                            && uc.Approved
                            && (uc.ExpiryDate == null || uc.ExpiryDate > now)));

                if (count < rc.MinimumRequired)
                {
                    warnings.Add(new CompetencyWarningResponse
                    {
                        CompetencyName = rc.Competency.Name,
                        MinimumRequired = rc.MinimumRequired,
                        CurrentCount = count,
                    });
                }
            }

            return warnings.Any() ? warnings : null;
        }

        private ShiftResponse Map(EventResourceUser shift) => new ShiftResponse
        {
            Id = shift.EventResourceUserId,
            EventResourceId = shift.EventResourceId,
            User = Map(shift.User),
            NeedsTraining = shift.User.Trainings.Any(t => t.ResourceTypeId == shift.Resource.ResourceTypeId && t.TrainingComplete.HasValue && !t.TrainingComplete.Value),
            StartTime = shift.StartTime,
            EndTime = shift.EndTime,
            Comment = shift.Comment,
        };

        private MessageResponse Map(EventResourceMessage message) => new MessageResponse
        {
            Id = message.EventResourceMessageId,
            EventResourceId = message.EventResourceId,
            CreatedBy = Map(message.CreatedByUser),
            Created = message.Created.AsUtc().ToIsoString(),
            Message = message.Message,
        };

        private ShiftUserResponse Map(User user) => new ShiftUserResponse
        {
            Id = user.UserId,
            PhoneNumber = user.UserName,
            FirstName = user.FirstName,
            LastName = user.LastName,
            FullName = $"{user.FirstName ?? ""} {user.LastName ?? ""}".Trim(),
            Trainings = user.Trainings?.Select(Map).ToList(),
        };

        private EventResource Map(ResourceRequest request)
        {
            var resource = new EventResource
            {
                ResourceTypeId = request.ResourceTypeId,
                StartTime = DateTime.Parse(request.StartTime),
                EndTime = DateTime.Parse(request.EndTime),
                MinimumStaff = request.MinimumStaff,
            };
            if (request.Id.HasValue)
            {
                resource.EventResourceId = request.Id.Value;
            }
            return resource;
        }

        public async Task<IEnumerable<MessageResponse>> GetMessages(int eventResourceId)
        {
            var messages = await DbContext.Messages
                .Include(m => m.CreatedByUser)
                .AsNoTracking()
                .Where(m => m.EventResourceId == eventResourceId)
                .ToListAsync();

            return messages.Select(Map).ToList();
        }

        public async Task<MessageResponse?> AddMessage(int id, MessageRequest request)
        {
            request.CreatedBy = CurrentUser.UserId;

            var message = new EventResourceMessage
            {
                Message = request.Message,
                EventResourceId = id,
                Created = DateTime.UtcNow,
                CreatedBy = request.CreatedBy,
            };
            DbContext.Messages.Add(message);
            await DbContext.SaveChangesAsync();

            var response = await DbContext.Messages
                .Include(m => m.CreatedByUser)
                .AsNoTracking()
                .SingleAsync(m => m.EventResourceMessageId == message.EventResourceMessageId);
            return Map(response);
        }

        public async Task<MessageResponse?> DeleteMessage(int id, int eventResourceId)
        {
            var message = await DbContext.Messages
                .Include(m => m.CreatedByUser)
                .SingleOrDefaultAsync(m => m.EventResourceId == eventResourceId && m.EventResourceMessageId == id);

            if (message == null) return null;

            DbContext.Remove(message);
            await DbContext.SaveChangesAsync();

            return Map(message);
        }

        public async Task<MinimumStaffResponse?> UpdateMinimumStaff(int id, MinimumStaffRequest request)
        {
            var eventResource = await DbContext.EventResource
                .SingleOrDefaultAsync(er => er.EventResourceId == id);

            if (eventResource == null) return null;

            eventResource.MinimumStaff = request.MinimumStaff;
            await DbContext.SaveChangesAsync();

            return new MinimumStaffResponse
            {
                EventResourceId = eventResource.EventResourceId,
                MinimumStaff = eventResource.MinimumStaff
            };
        }
    }
}
