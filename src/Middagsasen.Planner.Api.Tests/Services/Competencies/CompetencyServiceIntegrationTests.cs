using Microsoft.EntityFrameworkCore;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services.Competencies;
using Middagsasen.Planner.Api.Tests.Infrastructure;

namespace Middagsasen.Planner.Api.Tests.Services.Competencies
{
    [Collection("Database")]
    public class CompetencyServiceIntegrationTests
    {
        private readonly DatabaseFixture _fixture;

        public CompetencyServiceIntegrationTests(DatabaseFixture fixture)
        {
            _fixture = fixture;
        }

        private CompetencyService CreateService(PlannerDbContext context)
        {
            var repository = new CompetencyRepository(context);
            return new CompetencyService(repository);
        }

        private static string UniqueName(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

        private async Task<User> SeedUser(PlannerDbContext context, string? firstName = null, string? lastName = null)
        {
            var user = new User
            {
                UserName = $"+47{Random.Shared.Next(10000000, 99999999)}",
                FirstName = firstName ?? "Test",
                LastName = lastName ?? "User",
                Created = DateTime.UtcNow,
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();
            return user;
        }

        private async Task<ResourceType> SeedResourceType(PlannerDbContext context, string? name = null, int defaultStaff = 2)
        {
            var rt = new ResourceType
            {
                Name = name ?? UniqueName("RT"),
                DefaultStaff = defaultStaff,
            };
            context.ResourceTypes.Add(rt);
            await context.SaveChangesAsync();
            return rt;
        }

        private async Task<Competency> SeedCompetency(
            PlannerDbContext context,
            string? name = null,
            string? description = null,
            bool hasExpiry = false,
            bool inactive = false)
        {
            var competency = new Competency
            {
                Name = name ?? UniqueName("Comp"),
                Description = description ?? "Test competency",
                HasExpiry = hasExpiry,
                Inactive = inactive,
            };
            context.Competencies.Add(competency);
            await context.SaveChangesAsync();
            return competency;
        }

        #region Competency CRUD

        [Fact]
        public async Task CreateCompetency_PersistsToDatabase()
        {
            // Arrange
            var name = UniqueName("Create");
            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            var request = new CompetencyRequest
            {
                Name = name,
                Description = "A test competency",
                HasExpiry = true,
            };

            // Act
            var result = await service.CreateCompetency(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(name, result.Name);
            Assert.Equal("A test competency", result.Description);
            Assert.True(result.HasExpiry);

            // Verify in DB with fresh context
            using var verifyContext = _fixture.CreateContext();
            var dbEntity = await verifyContext.Competencies
                .AsNoTracking()
                .SingleOrDefaultAsync(c => c.CompetencyId == result.Id);
            Assert.NotNull(dbEntity);
            Assert.Equal(name, dbEntity.Name);
            Assert.Equal("A test competency", dbEntity.Description);
            Assert.True(dbEntity.HasExpiry);
            Assert.False(dbEntity.Inactive);
        }

        [Fact]
        public async Task CreateCompetency_WithResourceTypeCompetencies_CreatesAssociations()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var rt = await SeedResourceType(seedContext, name: UniqueName("RT-Assoc"));

            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            var request = new CompetencyRequest
            {
                Name = UniqueName("CompRT"),
                Description = "With RT",
                HasExpiry = false,
                ResourceTypeCompetencies = new List<ResourceTypeCompetencyRequest>
                {
                    new ResourceTypeCompetencyRequest { ResourceTypeId = rt.ResourceTypeId, MinimumRequired = 3 },
                },
            };

            // Act
            var result = await service.CreateCompetency(request);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result.ResourceTypes);
            Assert.Equal(rt.ResourceTypeId, result.ResourceTypes.First().ResourceTypeId);
            Assert.Equal(3, result.ResourceTypes.First().MinimumRequired);

            // Verify in DB
            using var verifyContext = _fixture.CreateContext();
            var rtcs = await verifyContext.ResourceTypeCompetencies
                .AsNoTracking()
                .Where(r => r.CompetencyId == result.Id && r.ResourceTypeId == rt.ResourceTypeId)
                .ToListAsync();
            Assert.Single(rtcs);
            Assert.Equal(3, rtcs.First().MinimumRequired);
        }

        [Fact]
        public async Task GetCompetencies_ReturnsOnlyActiveCompetencies()
        {
            // Arrange
            var activeName = UniqueName("Active");
            var inactiveName = UniqueName("Inactive");

            using var seedContext = _fixture.CreateContext();
            await SeedCompetency(seedContext, name: activeName, inactive: false);
            await SeedCompetency(seedContext, name: inactiveName, inactive: true);

            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act
            var result = (await service.GetCompetencies()).ToList();

            // Assert - only active competencies are returned
            Assert.Contains(result, c => c.Name == activeName);
            Assert.DoesNotContain(result, c => c.Name == inactiveName);
        }

        [Fact]
        public async Task GetCompetencies_ReturnsSortedByName()
        {
            // Arrange
            var zuluName = UniqueName("Zulu");
            var alphaName = UniqueName("Alpha");

            using var seedContext = _fixture.CreateContext();
            await SeedCompetency(seedContext, name: zuluName);
            await SeedCompetency(seedContext, name: alphaName);

            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act
            var result = (await service.GetCompetencies()).ToList();

            // Assert
            var zuluIndex = result.FindIndex(c => c.Name == zuluName);
            var alphaIndex = result.FindIndex(c => c.Name == alphaName);
            Assert.True(alphaIndex < zuluIndex, "Alpha should come before Zulu in sorted order");
        }

        [Fact]
        public async Task GetCompetencyById_ReturnsWithResourceTypesAndApprovers()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var rt = await SeedResourceType(seedContext, name: UniqueName("RT-Detail"));
            var user = await SeedUser(seedContext, "Approver", "Person");
            var competency = await SeedCompetency(seedContext, name: UniqueName("DetailComp"));

            // Add resource type competency
            seedContext.ResourceTypeCompetencies.Add(new ResourceTypeCompetency
            {
                CompetencyId = competency.CompetencyId,
                ResourceTypeId = rt.ResourceTypeId,
                MinimumRequired = 2,
            });
            await seedContext.SaveChangesAsync();

            // Add approver
            seedContext.CompetencyApprovers.Add(new CompetencyApprover
            {
                CompetencyId = competency.CompetencyId,
                UserId = user.UserId,
            });
            await seedContext.SaveChangesAsync();

            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act
            var result = await service.GetCompetencyById(competency.CompetencyId);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result.ResourceTypes);
            Assert.Equal(rt.ResourceTypeId, result.ResourceTypes.First().ResourceTypeId);
            Assert.Equal(2, result.ResourceTypes.First().MinimumRequired);
            Assert.Single(result.Approvers);
            Assert.Equal(user.UserId, result.Approvers.First().UserId);
        }

        [Fact]
        public async Task UpdateCompetency_UpdatesProperties()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var competency = await SeedCompetency(seedContext, name: UniqueName("Original"), description: "Old desc", hasExpiry: false);

            var updatedName = UniqueName("Updated");
            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            var request = new CompetencyRequest
            {
                Name = updatedName,
                Description = "New desc",
                HasExpiry = true,
            };

            // Act
            var result = await service.UpdateCompetency(competency.CompetencyId, request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(updatedName, result.Name);
            Assert.Equal("New desc", result.Description);
            Assert.True(result.HasExpiry);

            // Verify in DB
            using var verifyContext = _fixture.CreateContext();
            var dbEntity = await verifyContext.Competencies
                .AsNoTracking()
                .SingleAsync(c => c.CompetencyId == competency.CompetencyId);
            Assert.Equal(updatedName, dbEntity.Name);
            Assert.Equal("New desc", dbEntity.Description);
            Assert.True(dbEntity.HasExpiry);
        }

        [Fact]
        public async Task UpdateCompetency_UpdatesResourceTypeAssociations()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var rt1 = await SeedResourceType(seedContext, name: UniqueName("RT1"));
            var rt2 = await SeedResourceType(seedContext, name: UniqueName("RT2"));
            var competency = await SeedCompetency(seedContext, name: UniqueName("UpdateRT"));

            // Add initial association with rt1
            seedContext.ResourceTypeCompetencies.Add(new ResourceTypeCompetency
            {
                CompetencyId = competency.CompetencyId,
                ResourceTypeId = rt1.ResourceTypeId,
                MinimumRequired = 1,
            });
            await seedContext.SaveChangesAsync();

            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act - change rt1's MinimumRequired and add rt2
            var request = new CompetencyRequest
            {
                Name = competency.Name,
                Description = competency.Description,
                HasExpiry = competency.HasExpiry,
                ResourceTypeCompetencies = new List<ResourceTypeCompetencyRequest>
                {
                    new ResourceTypeCompetencyRequest { ResourceTypeId = rt1.ResourceTypeId, MinimumRequired = 5 },
                    new ResourceTypeCompetencyRequest { ResourceTypeId = rt2.ResourceTypeId, MinimumRequired = 2 },
                },
            };

            var result = await service.UpdateCompetency(competency.CompetencyId, request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.ResourceTypes.Count());

            // Verify in DB
            using var verifyContext = _fixture.CreateContext();
            var rtcs = await verifyContext.ResourceTypeCompetencies
                .AsNoTracking()
                .Where(r => r.CompetencyId == competency.CompetencyId)
                .OrderBy(r => r.ResourceTypeId)
                .ToListAsync();
            Assert.Equal(2, rtcs.Count);
            var rt1Assoc = rtcs.Single(r => r.ResourceTypeId == rt1.ResourceTypeId);
            var rt2Assoc = rtcs.Single(r => r.ResourceTypeId == rt2.ResourceTypeId);
            Assert.Equal(5, rt1Assoc.MinimumRequired);
            Assert.Equal(2, rt2Assoc.MinimumRequired);
        }

        [Fact]
        public async Task DeleteCompetency_SetsInactive()
        {
            // Arrange
            var name = UniqueName("ToDelete");
            using var seedContext = _fixture.CreateContext();
            var competency = await SeedCompetency(seedContext, name: name);

            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act
            var result = await service.DeleteCompetency(competency.CompetencyId);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Inactive);

            // Verify still in DB but marked inactive
            using var verifyContext = _fixture.CreateContext();
            var dbEntity = await verifyContext.Competencies
                .AsNoTracking()
                .SingleAsync(c => c.CompetencyId == competency.CompetencyId);
            Assert.True(dbEntity.Inactive);
            Assert.Equal(name, dbEntity.Name);
        }

        #endregion

        #region User Competencies

        [Fact]
        public async Task AddUserCompetency_CreatesWithApprovedFalse()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var user = await SeedUser(seedContext, "Comp", "User");
            var competency = await SeedCompetency(seedContext, name: UniqueName("UC-Add"));

            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            var request = new UserCompetencyRequest
            {
                UserId = user.UserId,
                CompetencyId = competency.CompetencyId,
            };

            // Act
            var result = await service.AddUserCompetency(request);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Approved);
            Assert.True(result.Created > DateTime.MinValue);
            Assert.Equal(user.UserId, result.UserId);
            Assert.Equal(competency.CompetencyId, result.CompetencyId);

            // Verify in DB
            using var verifyContext = _fixture.CreateContext();
            var dbEntity = await verifyContext.UserCompetencies
                .AsNoTracking()
                .SingleOrDefaultAsync(uc => uc.UserCompetencyId == result.Id);
            Assert.NotNull(dbEntity);
            Assert.False(dbEntity.Approved);
        }

        [Fact]
        public async Task GetUserCompetencies_ReturnsCompetenciesForUser()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var user = await SeedUser(seedContext, "Multi", "Comp");
            var comp1 = await SeedCompetency(seedContext, name: UniqueName("UC-Get1"));
            var comp2 = await SeedCompetency(seedContext, name: UniqueName("UC-Get2"));

            seedContext.UserCompetencies.Add(new UserCompetency
            {
                UserId = user.UserId,
                CompetencyId = comp1.CompetencyId,
                Approved = false,
                Created = DateTime.UtcNow,
            });
            seedContext.UserCompetencies.Add(new UserCompetency
            {
                UserId = user.UserId,
                CompetencyId = comp2.CompetencyId,
                Approved = false,
                Created = DateTime.UtcNow,
            });
            await seedContext.SaveChangesAsync();

            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act
            var result = (await service.GetUserCompetencies(user.UserId)).ToList();

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Contains(result, uc => uc.CompetencyId == comp1.CompetencyId);
            Assert.Contains(result, uc => uc.CompetencyId == comp2.CompetencyId);
        }

        [Fact]
        public async Task ApproveUserCompetency_SetsApprovedAndApprovedBy()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var user = await SeedUser(seedContext, "Approve", "Target");
            var approver = await SeedUser(seedContext, "Approve", "By");
            var competency = await SeedCompetency(seedContext, name: UniqueName("UC-Approve"));

            seedContext.UserCompetencies.Add(new UserCompetency
            {
                UserId = user.UserId,
                CompetencyId = competency.CompetencyId,
                Approved = false,
                Created = DateTime.UtcNow,
            });
            await seedContext.SaveChangesAsync();
            var ucId = seedContext.UserCompetencies
                .AsNoTracking()
                .Single(uc => uc.UserId == user.UserId && uc.CompetencyId == competency.CompetencyId)
                .UserCompetencyId;

            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act
            var result = await service.ApproveUserCompetency(ucId, approver.UserId, new ApproveCompetencyRequest());

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Approved);
            Assert.NotNull(result.ApprovedDate);
            Assert.Equal(approver.UserId, result.ApprovedById);

            // Verify in DB
            using var verifyContext = _fixture.CreateContext();
            var dbEntity = await verifyContext.UserCompetencies
                .AsNoTracking()
                .SingleAsync(uc => uc.UserCompetencyId == ucId);
            Assert.True(dbEntity.Approved);
            Assert.Equal(approver.UserId, dbEntity.ApprovedBy);
        }

        [Fact]
        public async Task ApproveUserCompetency_SetsExpiryDate()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var user = await SeedUser(seedContext, "Expiry", "User");
            var approver = await SeedUser(seedContext, "Expiry", "Approver");
            var competency = await SeedCompetency(seedContext, name: UniqueName("UC-Expiry"), hasExpiry: true);

            seedContext.UserCompetencies.Add(new UserCompetency
            {
                UserId = user.UserId,
                CompetencyId = competency.CompetencyId,
                Approved = false,
                Created = DateTime.UtcNow,
            });
            await seedContext.SaveChangesAsync();
            var ucId = seedContext.UserCompetencies
                .AsNoTracking()
                .Single(uc => uc.UserId == user.UserId && uc.CompetencyId == competency.CompetencyId)
                .UserCompetencyId;

            var expiryDate = DateTime.UtcNow.AddYears(1).Date;

            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act
            var result = await service.ApproveUserCompetency(ucId, approver.UserId, new ApproveCompetencyRequest
            {
                ExpiryDate = expiryDate,
            });

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expiryDate, result.ExpiryDate);

            // Verify in DB
            using var verifyContext = _fixture.CreateContext();
            var dbEntity = await verifyContext.UserCompetencies
                .AsNoTracking()
                .SingleAsync(uc => uc.UserCompetencyId == ucId);
            Assert.Equal(expiryDate, dbEntity.ExpiryDate);
        }

        [Fact]
        public async Task RevokeUserCompetency_ClearsApproval()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var user = await SeedUser(seedContext, "Revoke", "User");
            var approver = await SeedUser(seedContext, "Revoke", "Approver");
            var competency = await SeedCompetency(seedContext, name: UniqueName("UC-Revoke"));

            // Create an approved user competency
            seedContext.UserCompetencies.Add(new UserCompetency
            {
                UserId = user.UserId,
                CompetencyId = competency.CompetencyId,
                Approved = true,
                ApprovedDate = DateTime.UtcNow,
                ApprovedBy = approver.UserId,
                Created = DateTime.UtcNow,
            });
            await seedContext.SaveChangesAsync();
            var ucId = seedContext.UserCompetencies
                .AsNoTracking()
                .Single(uc => uc.UserId == user.UserId && uc.CompetencyId == competency.CompetencyId)
                .UserCompetencyId;

            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act
            var result = await service.RevokeUserCompetency(ucId);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Approved);
            Assert.Null(result.ApprovedDate);
            Assert.Null(result.ApprovedById);

            // Verify in DB
            using var verifyContext = _fixture.CreateContext();
            var dbEntity = await verifyContext.UserCompetencies
                .AsNoTracking()
                .SingleAsync(uc => uc.UserCompetencyId == ucId);
            Assert.False(dbEntity.Approved);
            Assert.Null(dbEntity.ApprovedDate);
            Assert.Null(dbEntity.ApprovedBy);
        }

        [Fact]
        public async Task GetUserCompetencyById_ReturnsCorrectRecord()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var user = await SeedUser(seedContext, "GetById", "User");
            var competency = await SeedCompetency(seedContext, name: UniqueName("UC-ById"));

            seedContext.UserCompetencies.Add(new UserCompetency
            {
                UserId = user.UserId,
                CompetencyId = competency.CompetencyId,
                Approved = false,
                Created = DateTime.UtcNow,
            });
            await seedContext.SaveChangesAsync();
            var ucId = seedContext.UserCompetencies
                .AsNoTracking()
                .Single(uc => uc.UserId == user.UserId && uc.CompetencyId == competency.CompetencyId)
                .UserCompetencyId;

            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act
            var result = await service.GetUserCompetencyById(ucId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(ucId, result.Id);
            Assert.Equal(user.UserId, result.UserId);
            Assert.Equal(competency.CompetencyId, result.CompetencyId);
            Assert.Equal(competency.Name, result.CompetencyName);
        }

        #endregion

        #region Resource Type Competencies

        [Fact]
        public async Task SetResourceTypeCompetencies_ReplacesExisting()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var rt = await SeedResourceType(seedContext, name: UniqueName("RT-Replace"));
            var comp1 = await SeedCompetency(seedContext, name: UniqueName("RTC-Old"));
            var comp2 = await SeedCompetency(seedContext, name: UniqueName("RTC-New"));

            using var context1 = _fixture.CreateContext();
            var service1 = CreateService(context1);

            // Set initial competencies
            await service1.SetResourceTypeCompetencies(rt.ResourceTypeId, new List<SetResourceTypeCompetencyRequest>
            {
                new SetResourceTypeCompetencyRequest { CompetencyId = comp1.CompetencyId, MinimumRequired = 1 },
            });

            // Act - replace with different values
            using var context2 = _fixture.CreateContext();
            var service2 = CreateService(context2);
            var result = (await service2.SetResourceTypeCompetencies(rt.ResourceTypeId, new List<SetResourceTypeCompetencyRequest>
            {
                new SetResourceTypeCompetencyRequest { CompetencyId = comp2.CompetencyId, MinimumRequired = 3 },
            })).ToList();

            // Assert
            Assert.Single(result);
            Assert.Equal(comp2.CompetencyId, result.First().CompetencyId);
            Assert.Equal(3, result.First().MinimumRequired);

            // Verify in DB - only new ones remain
            using var verifyContext = _fixture.CreateContext();
            var rtcs = await verifyContext.ResourceTypeCompetencies
                .AsNoTracking()
                .Where(r => r.ResourceTypeId == rt.ResourceTypeId)
                .ToListAsync();
            Assert.Single(rtcs);
            Assert.Equal(comp2.CompetencyId, rtcs.First().CompetencyId);
        }

        [Fact]
        public async Task GetResourceTypeCompetencies_ReturnsWithCompetencyNames()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var rt = await SeedResourceType(seedContext, name: UniqueName("RT-Names"));
            var compName = UniqueName("RTC-Named");
            var comp = await SeedCompetency(seedContext, name: compName);

            using var setContext = _fixture.CreateContext();
            var setService = CreateService(setContext);
            await setService.SetResourceTypeCompetencies(rt.ResourceTypeId, new List<SetResourceTypeCompetencyRequest>
            {
                new SetResourceTypeCompetencyRequest { CompetencyId = comp.CompetencyId, MinimumRequired = 2 },
            });

            // Act - fetch with a fresh service
            using var context = _fixture.CreateContext();
            var service = CreateService(context);
            var result = (await service.GetResourceTypeCompetencies(rt.ResourceTypeId)).ToList();

            // Assert
            Assert.Single(result);
            Assert.Equal(compName, result.First().CompetencyName);
            Assert.Equal(2, result.First().MinimumRequired);
        }

        #endregion

        #region Approvers

        [Fact]
        public async Task AddApprover_CreatesApproverLink()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var user = await SeedUser(seedContext, "New", "Approver");
            var competency = await SeedCompetency(seedContext, name: UniqueName("Approver-Add"));

            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act
            var result = await service.AddApprover(competency.CompetencyId, user.UserId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(user.UserId, result.UserId);

            // Verify in DB
            using var verifyContext = _fixture.CreateContext();
            var dbEntity = await verifyContext.CompetencyApprovers
                .AsNoTracking()
                .SingleOrDefaultAsync(ca => ca.CompetencyApproverId == result.Id);
            Assert.NotNull(dbEntity);
            Assert.Equal(competency.CompetencyId, dbEntity.CompetencyId);
            Assert.Equal(user.UserId, dbEntity.UserId);
            Assert.False(dbEntity.Inactive);
        }

        [Fact]
        public async Task RemoveApprover_SetsInactive()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var user = await SeedUser(seedContext, "Remove", "Approver");
            var competency = await SeedCompetency(seedContext, name: UniqueName("Approver-Remove"));

            seedContext.CompetencyApprovers.Add(new CompetencyApprover
            {
                CompetencyId = competency.CompetencyId,
                UserId = user.UserId,
            });
            await seedContext.SaveChangesAsync();
            var approverId = seedContext.CompetencyApprovers
                .AsNoTracking()
                .Single(ca => ca.CompetencyId == competency.CompetencyId && ca.UserId == user.UserId)
                .CompetencyApproverId;

            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act
            var removed = await service.RemoveApprover(approverId);

            // Assert
            Assert.True(removed);

            // Verify in DB - still exists but inactive
            using var verifyContext = _fixture.CreateContext();
            var dbEntity = await verifyContext.CompetencyApprovers
                .AsNoTracking()
                .SingleAsync(ca => ca.CompetencyApproverId == approverId);
            Assert.True(dbEntity.Inactive);
        }

        [Fact]
        public async Task IsApprover_ReturnsTrueForActiveApprover()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var user = await SeedUser(seedContext, "Is", "Approver");
            var competency = await SeedCompetency(seedContext, name: UniqueName("IsApprover-True"));

            seedContext.CompetencyApprovers.Add(new CompetencyApprover
            {
                CompetencyId = competency.CompetencyId,
                UserId = user.UserId,
                Inactive = false,
            });
            await seedContext.SaveChangesAsync();

            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act
            var result = await service.IsApprover(competency.CompetencyId, user.UserId);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task IsApprover_ReturnsFalseForInactiveApprover()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var user = await SeedUser(seedContext, "Inactive", "Approver");
            var competency = await SeedCompetency(seedContext, name: UniqueName("IsApprover-False"));

            seedContext.CompetencyApprovers.Add(new CompetencyApprover
            {
                CompetencyId = competency.CompetencyId,
                UserId = user.UserId,
                Inactive = true,
            });
            await seedContext.SaveChangesAsync();

            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act
            var result = await service.IsApprover(competency.CompetencyId, user.UserId);

            // Assert
            Assert.False(result);
        }

        #endregion
    }
}
