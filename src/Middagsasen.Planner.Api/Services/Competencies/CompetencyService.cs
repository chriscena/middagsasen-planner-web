using Middagsasen.Planner.Api.Data;

namespace Middagsasen.Planner.Api.Services.Competencies
{
    public class CompetencyService : ICompetencyService
    {
        public CompetencyService(ICompetencyRepository repository)
        {
            Repository = repository;
        }

        public ICompetencyRepository Repository { get; }

        // Competency CRUD

        public async Task<IEnumerable<CompetencyResponse>> GetCompetencies()
        {
            var competencies = await Repository.GetCompetencies();
            return competencies.Select(MapCompetency).OrderBy(c => c.Name).ToList();
        }

        public async Task<CompetencyResponse?> GetCompetencyById(int id)
        {
            var competency = await Repository.GetCompetencyById(id);
            return competency != null ? MapCompetency(competency) : null;
        }

        public async Task<CompetencyResponse> CreateCompetency(CompetencyRequest request)
        {
            var competency = new Competency
            {
                Name = request.Name,
                Description = request.Description,
                HasExpiry = request.HasExpiry,
            };

            competency = await Repository.AddCompetency(competency);

            if (request.ResourceTypeCompetencies?.Any() == true)
            {
                var rtcs = request.ResourceTypeCompetencies.Select(rtc => new ResourceTypeCompetency
                {
                    CompetencyId = competency.CompetencyId,
                    ResourceTypeId = rtc.ResourceTypeId,
                    MinimumRequired = rtc.MinimumRequired,
                }).ToList();

                foreach (var resourceTypeId in rtcs.Select(r => r.ResourceTypeId).Distinct())
                {
                    var existing = await Repository.GetResourceTypeCompetencies(resourceTypeId);
                    var merged = existing.Select(e => new ResourceTypeCompetency
                    {
                        ResourceTypeId = e.ResourceTypeId,
                        CompetencyId = e.CompetencyId,
                        MinimumRequired = e.MinimumRequired,
                    }).Concat(rtcs.Where(r => r.ResourceTypeId == resourceTypeId)).ToList();

                    await Repository.SetResourceTypeCompetencies(resourceTypeId, merged);
                }
            }
            else if (request.ResourceTypeIds?.Any() == true)
            {
                foreach (var resourceTypeId in request.ResourceTypeIds)
                {
                    var existing = await Repository.GetResourceTypeCompetencies(resourceTypeId);
                    var merged = existing.Select(e => new ResourceTypeCompetency
                    {
                        ResourceTypeId = e.ResourceTypeId,
                        CompetencyId = e.CompetencyId,
                        MinimumRequired = e.MinimumRequired,
                    }).Append(new ResourceTypeCompetency
                    {
                        CompetencyId = competency.CompetencyId,
                        ResourceTypeId = resourceTypeId,
                        MinimumRequired = 1,
                    }).ToList();

                    await Repository.SetResourceTypeCompetencies(resourceTypeId, merged);
                }
            }

            // Re-fetch to include navigation properties
            var result = await Repository.GetCompetencyById(competency.CompetencyId);
            return MapCompetency(result!);
        }

        public async Task<CompetencyResponse?> UpdateCompetency(int id, CompetencyRequest request)
        {
            var competency = await Repository.GetCompetencyById(id);
            if (competency == null) return null;

            competency.Name = request.Name;
            competency.Description = request.Description;
            competency.HasExpiry = request.HasExpiry;

            await Repository.SaveChangesAsync();

            // Update resource type competency associations
            if (request.ResourceTypeCompetencies != null)
            {
                // Find all resource types currently associated with this competency
                var currentResourceTypeIds = competency.ResourceTypeCompetencies
                    .Select(rtc => rtc.ResourceTypeId)
                    .ToHashSet();

                var newResourceTypeIds = request.ResourceTypeCompetencies
                    .Select(rtc => rtc.ResourceTypeId)
                    .ToHashSet();

                var allResourceTypeIds = currentResourceTypeIds.Union(newResourceTypeIds).ToList();

                foreach (var resourceTypeId in allResourceTypeIds)
                {
                    var existing = await Repository.GetResourceTypeCompetencies(resourceTypeId);

                    // Remove entries for this competency, then add new one if present
                    var withoutCurrent = existing
                        .Where(e => e.CompetencyId != id)
                        .Select(e => new ResourceTypeCompetency
                        {
                            ResourceTypeId = e.ResourceTypeId,
                            CompetencyId = e.CompetencyId,
                            MinimumRequired = e.MinimumRequired,
                        }).ToList();

                    var newEntry = request.ResourceTypeCompetencies
                        .FirstOrDefault(rtc => rtc.ResourceTypeId == resourceTypeId);

                    if (newEntry != null)
                    {
                        withoutCurrent.Add(new ResourceTypeCompetency
                        {
                            CompetencyId = id,
                            ResourceTypeId = newEntry.ResourceTypeId,
                            MinimumRequired = newEntry.MinimumRequired,
                        });
                    }

                    await Repository.SetResourceTypeCompetencies(resourceTypeId, withoutCurrent);
                }
            }

            // Re-fetch to include updated navigation properties
            var result = await Repository.GetCompetencyById(id);
            return MapCompetency(result!);
        }

        public async Task<CompetencyResponse?> DeleteCompetency(int id)
        {
            var competency = await Repository.GetCompetencyById(id);
            if (competency == null) return null;

            competency.Inactive = true;
            await Repository.SaveChangesAsync();

            return MapCompetency(competency);
        }

        // User competencies

        public async Task<IEnumerable<UserCompetencyResponse>> GetUserCompetencies(int userId)
        {
            var userCompetencies = await Repository.GetUserCompetencies(userId);
            return userCompetencies.Select(MapUserCompetency).ToList();
        }

        public async Task<UserCompetencyResponse?> GetUserCompetencyById(int id)
        {
            var userCompetency = await Repository.GetUserCompetencyById(id);
            return userCompetency != null ? MapUserCompetency(userCompetency) : null;
        }

        public async Task<UserCompetencyResponse?> AddUserCompetency(UserCompetencyRequest request)
        {
            var userCompetency = new UserCompetency
            {
                UserId = request.UserId,
                CompetencyId = request.CompetencyId,
                Approved = false,
                Created = DateTime.UtcNow,
            };

            userCompetency = await Repository.AddUserCompetency(userCompetency);

            // Re-fetch to include navigation properties
            var result = await Repository.GetUserCompetencyById(userCompetency.UserCompetencyId);
            return result != null ? MapUserCompetency(result) : null;
        }

        public async Task<UserCompetencyResponse?> ApproveUserCompetency(int userCompetencyId, int approvedByUserId, ApproveCompetencyRequest request)
        {
            var userCompetency = await Repository.GetUserCompetencyById(userCompetencyId);
            if (userCompetency == null) return null;

            userCompetency.Approved = true;
            userCompetency.ApprovedDate = DateTime.UtcNow;
            userCompetency.ApprovedBy = approvedByUserId;
            userCompetency.ExpiryDate = request.ExpiryDate;

            await Repository.SaveChangesAsync();

            // Re-fetch to include updated navigation properties
            var result = await Repository.GetUserCompetencyById(userCompetencyId);
            return result != null ? MapUserCompetency(result) : null;
        }

        public async Task<UserCompetencyResponse?> RevokeUserCompetency(int userCompetencyId)
        {
            var userCompetency = await Repository.GetUserCompetencyById(userCompetencyId);
            if (userCompetency == null) return null;

            userCompetency.Approved = false;
            userCompetency.ApprovedDate = null;
            userCompetency.ApprovedBy = null;

            await Repository.SaveChangesAsync();

            // Re-fetch to include updated navigation properties
            var result = await Repository.GetUserCompetencyById(userCompetencyId);
            return result != null ? MapUserCompetency(result) : null;
        }

        // Resource type competencies

        public async Task<IEnumerable<ResourceTypeCompetencyResponse>> GetResourceTypeCompetencies(int resourceTypeId)
        {
            var competencies = await Repository.GetResourceTypeCompetenciesWithCompetency(resourceTypeId);
            return competencies.Select(MapResourceTypeCompetency).ToList();
        }

        public async Task<IEnumerable<ResourceTypeCompetencyResponse>> SetResourceTypeCompetencies(int resourceTypeId, IEnumerable<SetResourceTypeCompetencyRequest> requirements)
        {
            var existing = await Repository.GetResourceTypeCompetencies(resourceTypeId);

            var merged = new List<ResourceTypeCompetency>();

            foreach (var req in requirements)
            {
                merged.Add(new ResourceTypeCompetency
                {
                    ResourceTypeId = resourceTypeId,
                    CompetencyId = req.CompetencyId,
                    MinimumRequired = req.MinimumRequired,
                });
            }

            await Repository.SetResourceTypeCompetencies(resourceTypeId, merged);

            // Re-fetch with navigation properties loaded
            var updated = await Repository.GetResourceTypeCompetenciesWithCompetency(resourceTypeId);
            return updated.Select(MapResourceTypeCompetency).ToList();
        }

        // Approvers

        public async Task<CompetencyApproverResponse?> AddApprover(int competencyId, int userId)
        {
            var competency = await Repository.GetCompetencyById(competencyId);
            if (competency == null) return null;

            var approver = new CompetencyApprover
            {
                CompetencyId = competencyId,
                UserId = userId,
            };

            approver = await Repository.AddApprover(approver);

            // Re-fetch to include navigation properties
            var result = await Repository.GetApproverById(approver.CompetencyApproverId);
            return result != null ? MapApprover(result) : null;
        }

        public async Task<bool> RemoveApprover(int approverId)
        {
            var approver = await Repository.GetApproverById(approverId);
            if (approver == null) return false;

            approver.Inactive = true;
            await Repository.SaveChangesAsync();

            return true;
        }

        public async Task<bool> IsApprover(int competencyId, int userId)
        {
            return await Repository.IsApprover(competencyId, userId);
        }

        // Mapping methods

        private CompetencyResponse MapCompetency(Competency competency) => new CompetencyResponse
        {
            Id = competency.CompetencyId,
            Name = competency.Name,
            Description = competency.Description,
            HasExpiry = competency.HasExpiry,
            Inactive = competency.Inactive,
            ResourceTypes = competency.ResourceTypeCompetencies?
                .Select(MapResourceType).ToList() ?? new List<CompetencyResourceTypeResponse>(),
            Approvers = competency.CompetencyApprovers?
                .Where(ca => !ca.Inactive)
                .Select(MapApprover).ToList() ?? new List<CompetencyApproverResponse>(),
        };

        private CompetencyResourceTypeResponse MapResourceType(ResourceTypeCompetency rtc) => new CompetencyResourceTypeResponse
        {
            ResourceTypeId = rtc.ResourceTypeId,
            ResourceTypeName = rtc.ResourceType?.Name ?? "",
            MinimumRequired = rtc.MinimumRequired,
        };

        private CompetencyApproverResponse MapApprover(CompetencyApprover approver) => new CompetencyApproverResponse
        {
            Id = approver.CompetencyApproverId,
            UserId = approver.UserId,
            FullName = MapFullName(approver.User?.FirstName, approver.User?.LastName),
        };

        private UserCompetencyResponse MapUserCompetency(UserCompetency uc) => new UserCompetencyResponse
        {
            Id = uc.UserCompetencyId,
            UserId = uc.UserId,
            UserFullName = MapFullName(uc.User?.FirstName, uc.User?.LastName),
            CompetencyId = uc.CompetencyId,
            CompetencyName = uc.Competency?.Name ?? "",
            Approved = uc.Approved,
            ApprovedDate = uc.ApprovedDate,
            ApprovedById = uc.ApprovedBy,
            ApprovedByName = MapFullName(uc.ApprovedByUser?.FirstName, uc.ApprovedByUser?.LastName),
            ExpiryDate = uc.ExpiryDate,
            IsExpired = uc.ExpiryDate != null && uc.ExpiryDate < DateTime.UtcNow,
            Created = uc.Created,
        };

        private ResourceTypeCompetencyResponse MapResourceTypeCompetency(ResourceTypeCompetency rtc) => new ResourceTypeCompetencyResponse
        {
            CompetencyId = rtc.CompetencyId,
            CompetencyName = rtc.Competency?.Name ?? "",
            MinimumRequired = rtc.MinimumRequired,
        };

        private string MapFullName(string? firstName, string? lastName)
        {
            return $"{firstName ?? ""} {lastName ?? ""}".Trim();
        }
    }
}
