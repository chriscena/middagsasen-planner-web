using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services.Competencies;
using NSubstitute;

namespace Middagsasen.Planner.Api.Tests.Services.Competencies
{
    public class CompetencyServiceTests
    {
        private readonly ICompetencyRepository _repository;
        private readonly CompetencyService _sut;

        public CompetencyServiceTests()
        {
            _repository = Substitute.For<ICompetencyRepository>();
            _sut = new CompetencyService(_repository);
        }

        // Helper to create a Competency entity with navigation properties
        private static Competency CreateCompetency(int id, string name, string? description = null, bool hasExpiry = false)
        {
            return new Competency
            {
                CompetencyId = id,
                Name = name,
                Description = description,
                HasExpiry = hasExpiry,
                ResourceTypeCompetencies = new List<ResourceTypeCompetency>(),
                CompetencyApprovers = new List<CompetencyApprover>(),
            };
        }

        #region GetCompetencies

        [Fact]
        public async Task GetCompetencies_ReturnsMappedCompetenciesSortedByName()
        {
            // Arrange
            var competencies = new List<Competency>
            {
                CreateCompetency(1, "Zulu"),
                CreateCompetency(2, "Alpha"),
                CreateCompetency(3, "Mike"),
            };
            _repository.GetCompetencies().Returns(competencies);

            // Act
            var result = (await _sut.GetCompetencies()).ToList();

            // Assert
            Assert.Equal(3, result.Count);
            Assert.Equal("Alpha", result[0].Name);
            Assert.Equal("Mike", result[1].Name);
            Assert.Equal("Zulu", result[2].Name);
            Assert.Equal(2, result[0].Id);
        }

        [Fact]
        public async Task GetCompetencies_ReturnsEmptyList_WhenNoCompetenciesExist()
        {
            // Arrange
            _repository.GetCompetencies().Returns(new List<Competency>());

            // Act
            var result = (await _sut.GetCompetencies()).ToList();

            // Assert
            Assert.Empty(result);
        }

        #endregion

        #region GetCompetencyById

        [Fact]
        public async Task GetCompetencyById_ReturnsMappedCompetency_WhenFound()
        {
            // Arrange
            var competency = CreateCompetency(1, "First Aid", "Basic first aid", true);
            _repository.GetCompetencyById(1).Returns(competency);

            // Act
            var result = await _sut.GetCompetencyById(1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.Id);
            Assert.Equal("First Aid", result.Name);
            Assert.Equal("Basic first aid", result.Description);
            Assert.True(result.HasExpiry);
        }

        [Fact]
        public async Task GetCompetencyById_ReturnsNull_WhenNotFound()
        {
            // Arrange
            _repository.GetCompetencyById(999).Returns((Competency?)null);

            // Act
            var result = await _sut.GetCompetencyById(999);

            // Assert
            Assert.Null(result);
        }

        #endregion

        #region CreateCompetency

        [Fact]
        public async Task CreateCompetency_CreatesCompetencyWithCorrectProperties()
        {
            // Arrange
            var request = new CompetencyRequest
            {
                Name = "Fire Safety",
                Description = "Fire safety training",
                HasExpiry = true,
            };

            var savedCompetency = CreateCompetency(10, "Fire Safety", "Fire safety training", true);

            _repository.AddCompetency(Arg.Any<Competency>()).Returns(callInfo =>
            {
                var c = callInfo.Arg<Competency>();
                c.CompetencyId = 10;
                return c;
            });
            _repository.GetCompetencyById(10).Returns(savedCompetency);

            // Act
            var result = await _sut.CreateCompetency(request);

            // Assert
            await _repository.Received(1).AddCompetency(Arg.Is<Competency>(c =>
                c.Name == "Fire Safety" &&
                c.Description == "Fire safety training" &&
                c.HasExpiry == true));
            Assert.Equal("Fire Safety", result.Name);
        }

        [Fact]
        public async Task CreateCompetency_CreatesResourceTypeAssociations_WhenResourceTypeCompetenciesProvided()
        {
            // Arrange
            var request = new CompetencyRequest
            {
                Name = "Lift Cert",
                HasExpiry = false,
                ResourceTypeCompetencies = new List<ResourceTypeCompetencyRequest>
                {
                    new ResourceTypeCompetencyRequest { ResourceTypeId = 1, MinimumRequired = 2 },
                },
            };

            _repository.AddCompetency(Arg.Any<Competency>()).Returns(callInfo =>
            {
                var c = callInfo.Arg<Competency>();
                c.CompetencyId = 5;
                return c;
            });
            _repository.GetResourceTypeCompetencies(1).Returns(new List<ResourceTypeCompetency>());
            _repository.GetCompetencyById(5).Returns(CreateCompetency(5, "Lift Cert"));

            // Act
            await _sut.CreateCompetency(request);

            // Assert
            await _repository.Received(1).SetResourceTypeCompetencies(1, Arg.Is<List<ResourceTypeCompetency>>(list =>
                list.Any(r => r.CompetencyId == 5 && r.ResourceTypeId == 1 && r.MinimumRequired == 2)));
        }

        [Fact]
        public async Task CreateCompetency_DoesNotCreateResourceTypeAssociations_WhenNotProvided()
        {
            // Arrange
            var request = new CompetencyRequest
            {
                Name = "Basic",
                HasExpiry = false,
            };

            _repository.AddCompetency(Arg.Any<Competency>()).Returns(callInfo =>
            {
                var c = callInfo.Arg<Competency>();
                c.CompetencyId = 6;
                return c;
            });
            _repository.GetCompetencyById(6).Returns(CreateCompetency(6, "Basic"));

            // Act
            await _sut.CreateCompetency(request);

            // Assert
            await _repository.DidNotReceive().SetResourceTypeCompetencies(Arg.Any<int>(), Arg.Any<IEnumerable<ResourceTypeCompetency>>());
        }

        #endregion

        #region UpdateCompetency

        [Fact]
        public async Task UpdateCompetency_UpdatesExistingCompetencyProperties()
        {
            // Arrange
            var competency = CreateCompetency(1, "Old Name", "Old Desc", false);
            _repository.GetCompetencyById(1).Returns(competency);

            var request = new CompetencyRequest
            {
                Name = "New Name",
                Description = "New Desc",
                HasExpiry = true,
            };

            // Act
            var result = await _sut.UpdateCompetency(1, request);

            // Assert
            Assert.Equal("New Name", competency.Name);
            Assert.Equal("New Desc", competency.Description);
            Assert.True(competency.HasExpiry);
            await _repository.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task UpdateCompetency_ReturnsNull_WhenCompetencyNotFound()
        {
            // Arrange
            _repository.GetCompetencyById(999).Returns((Competency?)null);

            // Act
            var result = await _sut.UpdateCompetency(999, new CompetencyRequest { Name = "X" });

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task UpdateCompetency_UpdatesResourceTypeCompetencyAssociations()
        {
            // Arrange
            var competency = CreateCompetency(1, "Cert");
            competency.ResourceTypeCompetencies = new List<ResourceTypeCompetency>
            {
                new ResourceTypeCompetency { ResourceTypeId = 10, CompetencyId = 1, MinimumRequired = 1 },
                new ResourceTypeCompetency { ResourceTypeId = 20, CompetencyId = 1, MinimumRequired = 2 },
            };

            // First call returns the competency with old associations, second call returns updated
            _repository.GetCompetencyById(1).Returns(competency, CreateCompetency(1, "Cert"));

            // Existing resource type competencies for each resource type
            _repository.GetResourceTypeCompetencies(10).Returns(new List<ResourceTypeCompetency>
            {
                new ResourceTypeCompetency { ResourceTypeId = 10, CompetencyId = 1, MinimumRequired = 1 },
                new ResourceTypeCompetency { ResourceTypeId = 10, CompetencyId = 99, MinimumRequired = 3 },
            });
            _repository.GetResourceTypeCompetencies(20).Returns(new List<ResourceTypeCompetency>
            {
                new ResourceTypeCompetency { ResourceTypeId = 20, CompetencyId = 1, MinimumRequired = 2 },
            });
            _repository.GetResourceTypeCompetencies(30).Returns(new List<ResourceTypeCompetency>());

            var request = new CompetencyRequest
            {
                Name = "Cert",
                ResourceTypeCompetencies = new List<ResourceTypeCompetencyRequest>
                {
                    // Keep RT 10 with new minimum, add RT 30, remove RT 20
                    new ResourceTypeCompetencyRequest { ResourceTypeId = 10, MinimumRequired = 5 },
                    new ResourceTypeCompetencyRequest { ResourceTypeId = 30, MinimumRequired = 1 },
                },
            };

            // Act
            await _sut.UpdateCompetency(1, request);

            // Assert - RT 10 should keep other competencies and update this one
            await _repository.Received(1).SetResourceTypeCompetencies(10, Arg.Is<List<ResourceTypeCompetency>>(list =>
                list.Count == 2 &&
                list.Any(r => r.CompetencyId == 99 && r.MinimumRequired == 3) &&
                list.Any(r => r.CompetencyId == 1 && r.MinimumRequired == 5)));

            // RT 20 should have this competency removed
            await _repository.Received(1).SetResourceTypeCompetencies(20, Arg.Is<List<ResourceTypeCompetency>>(list =>
                list.All(r => r.CompetencyId != 1)));

            // RT 30 should have this competency added
            await _repository.Received(1).SetResourceTypeCompetencies(30, Arg.Is<List<ResourceTypeCompetency>>(list =>
                list.Any(r => r.CompetencyId == 1 && r.MinimumRequired == 1)));
        }

        #endregion

        #region DeleteCompetency

        [Fact]
        public async Task DeleteCompetency_SetsInactiveToTrue()
        {
            // Arrange
            var competency = CreateCompetency(1, "ToDelete");
            _repository.GetCompetencyById(1).Returns(competency);

            // Act
            var result = await _sut.DeleteCompetency(1);

            // Assert
            Assert.True(competency.Inactive);
            await _repository.Received(1).SaveChangesAsync();
            Assert.NotNull(result);
        }

        [Fact]
        public async Task DeleteCompetency_ReturnsNull_WhenNotFound()
        {
            // Arrange
            _repository.GetCompetencyById(999).Returns((Competency?)null);

            // Act
            var result = await _sut.DeleteCompetency(999);

            // Assert
            Assert.Null(result);
        }

        #endregion

        #region AddUserCompetency

        [Fact]
        public async Task AddUserCompetency_CreatesUserCompetencyWithApprovedFalse()
        {
            // Arrange
            var request = new UserCompetencyRequest { UserId = 1, CompetencyId = 2 };

            _repository.AddUserCompetency(Arg.Any<UserCompetency>()).Returns(callInfo =>
            {
                var uc = callInfo.Arg<UserCompetency>();
                uc.UserCompetencyId = 100;
                return uc;
            });
            _repository.GetUserCompetencyById(100).Returns(new UserCompetency
            {
                UserCompetencyId = 100,
                UserId = 1,
                CompetencyId = 2,
                Approved = false,
                Created = DateTime.UtcNow,
            });

            // Act
            var result = await _sut.AddUserCompetency(request);

            // Assert
            await _repository.Received(1).AddUserCompetency(Arg.Is<UserCompetency>(uc =>
                uc.UserId == 1 &&
                uc.CompetencyId == 2 &&
                uc.Approved == false));
            Assert.NotNull(result);
            Assert.False(result.Approved);
        }

        [Fact]
        public async Task AddUserCompetency_SetsCreatedToApproximatelyUtcNow()
        {
            // Arrange
            var request = new UserCompetencyRequest { UserId = 1, CompetencyId = 2 };
            var before = DateTime.UtcNow;

            _repository.AddUserCompetency(Arg.Any<UserCompetency>()).Returns(callInfo =>
            {
                var uc = callInfo.Arg<UserCompetency>();
                uc.UserCompetencyId = 100;
                return uc;
            });
            _repository.GetUserCompetencyById(100).Returns(callInfo =>
            {
                return new UserCompetency
                {
                    UserCompetencyId = 100,
                    UserId = 1,
                    CompetencyId = 2,
                    Approved = false,
                    Created = DateTime.UtcNow,
                };
            });

            // Act
            await _sut.AddUserCompetency(request);
            var after = DateTime.UtcNow;

            // Assert
            await _repository.Received(1).AddUserCompetency(Arg.Is<UserCompetency>(uc =>
                uc.Created >= before && uc.Created <= after));
        }

        #endregion

        #region ApproveUserCompetency

        [Fact]
        public async Task ApproveUserCompetency_SetsApprovedTrueAndApprovedDateAndApprovedBy()
        {
            // Arrange
            var userCompetency = new UserCompetency
            {
                UserCompetencyId = 1,
                UserId = 10,
                CompetencyId = 20,
                Approved = false,
            };
            _repository.GetUserCompetencyById(1).Returns(userCompetency);
            var request = new ApproveCompetencyRequest { ExpiryDate = null };
            var before = DateTime.UtcNow;

            // Act
            var result = await _sut.ApproveUserCompetency(1, 50, request);

            // Assert
            Assert.True(userCompetency.Approved);
            Assert.NotNull(userCompetency.ApprovedDate);
            Assert.True(userCompetency.ApprovedDate >= before && userCompetency.ApprovedDate <= DateTime.UtcNow);
            Assert.Equal(50, userCompetency.ApprovedBy);
            await _repository.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task ApproveUserCompetency_SetsExpiryDate_WhenProvided()
        {
            // Arrange
            var userCompetency = new UserCompetency
            {
                UserCompetencyId = 1,
                UserId = 10,
                CompetencyId = 20,
                Approved = false,
            };
            _repository.GetUserCompetencyById(1).Returns(userCompetency);
            var expiryDate = new DateTime(2027, 6, 15, 0, 0, 0, DateTimeKind.Utc);
            var request = new ApproveCompetencyRequest { ExpiryDate = expiryDate };

            // Act
            await _sut.ApproveUserCompetency(1, 50, request);

            // Assert
            Assert.Equal(expiryDate, userCompetency.ExpiryDate);
        }

        [Fact]
        public async Task ApproveUserCompetency_ReturnsNull_WhenNotFound()
        {
            // Arrange
            _repository.GetUserCompetencyById(999).Returns((UserCompetency?)null);

            // Act
            var result = await _sut.ApproveUserCompetency(999, 1, new ApproveCompetencyRequest());

            // Assert
            Assert.Null(result);
        }

        #endregion

        #region RevokeUserCompetency

        [Fact]
        public async Task RevokeUserCompetency_SetsApprovedFalseAndClearsApprovedDateAndApprovedBy()
        {
            // Arrange
            var userCompetency = new UserCompetency
            {
                UserCompetencyId = 1,
                Approved = true,
                ApprovedDate = DateTime.UtcNow.AddDays(-30),
                ApprovedBy = 50,
            };
            _repository.GetUserCompetencyById(1).Returns(userCompetency);

            // Act
            await _sut.RevokeUserCompetency(1);

            // Assert
            Assert.False(userCompetency.Approved);
            Assert.Null(userCompetency.ApprovedDate);
            Assert.Null(userCompetency.ApprovedBy);
            await _repository.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task RevokeUserCompetency_ReturnsNull_WhenNotFound()
        {
            // Arrange
            _repository.GetUserCompetencyById(999).Returns((UserCompetency?)null);

            // Act
            var result = await _sut.RevokeUserCompetency(999);

            // Assert
            Assert.Null(result);
        }

        #endregion

        #region AddApprover

        [Fact]
        public async Task AddApprover_CreatesApproverLinkingUserToCompetency()
        {
            // Arrange
            var competency = CreateCompetency(1, "Test");
            _repository.GetCompetencyById(1).Returns(competency);
            _repository.AddApprover(Arg.Any<CompetencyApprover>()).Returns(callInfo =>
            {
                var a = callInfo.Arg<CompetencyApprover>();
                a.CompetencyApproverId = 10;
                return a;
            });
            _repository.GetApproverById(10).Returns(new CompetencyApprover
            {
                CompetencyApproverId = 10,
                CompetencyId = 1,
                UserId = 5,
                User = new User { FirstName = "John", LastName = "Doe" },
            });

            // Act
            var result = await _sut.AddApprover(1, 5);

            // Assert
            await _repository.Received(1).AddApprover(Arg.Is<CompetencyApprover>(a =>
                a.CompetencyId == 1 && a.UserId == 5));
            Assert.NotNull(result);
            Assert.Equal(10, result.Id);
            Assert.Equal(5, result.UserId);
        }

        [Fact]
        public async Task AddApprover_ReturnsNull_WhenCompetencyNotFound()
        {
            // Arrange
            _repository.GetCompetencyById(999).Returns((Competency?)null);

            // Act
            var result = await _sut.AddApprover(999, 1);

            // Assert
            Assert.Null(result);
        }

        #endregion

        #region RemoveApprover

        [Fact]
        public async Task RemoveApprover_SetsInactiveTrue()
        {
            // Arrange
            var approver = new CompetencyApprover
            {
                CompetencyApproverId = 1,
                CompetencyId = 10,
                UserId = 5,
                Inactive = false,
            };
            _repository.GetApproverById(1).Returns(approver);

            // Act
            var result = await _sut.RemoveApprover(1);

            // Assert
            Assert.True(result);
            Assert.True(approver.Inactive);
            await _repository.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task RemoveApprover_ReturnsFalse_WhenNotFound()
        {
            // Arrange
            _repository.GetApproverById(999).Returns((CompetencyApprover?)null);

            // Act
            var result = await _sut.RemoveApprover(999);

            // Assert
            Assert.False(result);
        }

        #endregion
    }
}
