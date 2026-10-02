using Microsoft.EntityFrameworkCore;
using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services;
using Middagsasen.Planner.Api.Services.ResourceTypes;
using Middagsasen.Planner.Api.Services.SmsSender;
using Middagsasen.Planner.Api.Services.Storage;
using Middagsasen.Planner.Api.Tests.Infrastructure;
using NSubstitute;

namespace Middagsasen.Planner.Api.Tests.Services.ResourceTypes
{
    [Collection("Database")]
    public class ResourceTypesServiceIntegrationTests
    {
        private readonly DatabaseFixture _fixture;
        private readonly ISmsSender _smsSender;
        private readonly IStorageService _storageService;

        public ResourceTypesServiceIntegrationTests(DatabaseFixture fixture)
        {
            _fixture = fixture;
            _smsSender = Substitute.For<ISmsSender>();
            _storageService = Substitute.For<IStorageService>();
        }

        private static ICurrentUserService MockCurrentUser(int userId, bool isAdmin = false)
        {
            var mock = Substitute.For<ICurrentUserService>();
            mock.UserId.Returns(userId);
            mock.IsAdmin.Returns(isAdmin);
            return mock;
        }

        private ResourceTypesService CreateService(PlannerDbContext context, int userId = 0, bool isAdmin = false)
        {
            var currentUser = MockCurrentUser(userId, isAdmin);
            return new ResourceTypesService(context, _smsSender, _storageService, currentUser);
        }

        private static string UniqueName(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

        private async Task<User> SeedUser(PlannerDbContext context, string? firstName = null, string? lastName = null)
        {
            var user = new User
            {
                UserName = UniqueName("user"),
                FirstName = firstName ?? "Test",
                LastName = lastName ?? "User",
                Created = DateTime.UtcNow,
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();
            return user;
        }

        private async Task<ResourceType> SeedResourceType(
            PlannerDbContext context,
            string? name = null,
            int defaultStaff = 2,
            bool inactive = false,
            List<int>? trainerUserIds = null)
        {
            var rt = new ResourceType
            {
                Name = name ?? UniqueName("RT"),
                DefaultStaff = defaultStaff,
                Inactive = inactive,
            };

            if (trainerUserIds != null)
            {
                foreach (var userId in trainerUserIds)
                {
                    rt.Trainers.Add(new ResourceTypeTrainer { UserId = userId });
                }
            }

            context.ResourceTypes.Add(rt);
            await context.SaveChangesAsync();
            return rt;
        }

        #region ResourceType CRUD

        [Fact]
        public async Task CreateResourceType_PersistsToDatabase()
        {
            // Arrange
            var name = UniqueName("Create");
            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            var request = new ResourceTypeRequest
            {
                Name = name,
                DefaultStaff = 3,
                NotificationMessage = "Test notification",
            };

            // Act
            var result = await service.CreateResourceType(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(name, result.Name);
            Assert.Equal(3, result.DefaultStaff);

            // Verify in DB with fresh context
            using var verifyContext = _fixture.CreateContext();
            var dbEntity = await verifyContext.ResourceTypes
                .AsNoTracking()
                .SingleOrDefaultAsync(r => r.ResourceTypeId == result.Id);
            Assert.NotNull(dbEntity);
            Assert.Equal(name, dbEntity.Name);
            Assert.Equal(3, dbEntity.DefaultStaff);
            Assert.Equal("Test notification", dbEntity.NotificationMessage);
            Assert.False(dbEntity.Inactive);
        }

        [Fact]
        public async Task GetResourceTypes_ReturnsOnlyActiveResourceTypes()
        {
            // Arrange - seed active and inactive resource types
            var activeName = UniqueName("Active");
            var inactiveName = UniqueName("Inactive");

            using var seedContext = _fixture.CreateContext();
            await SeedResourceType(seedContext, name: activeName, inactive: false);
            await SeedResourceType(seedContext, name: inactiveName, inactive: true);

            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act
            var result = (await service.GetResourceTypes()).ToList();

            // Assert
            Assert.Contains(result, r => r.Name == activeName);
            Assert.DoesNotContain(result, r => r.Name == inactiveName);
        }

        [Fact]
        public async Task GetResourceTypeById_ReturnsCorrectResourceType()
        {
            // Arrange
            var name = UniqueName("GetById");
            using var seedContext = _fixture.CreateContext();
            var seeded = await SeedResourceType(seedContext, name: name, defaultStaff: 5);

            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act
            var result = await service.GetResourceTypeById(seeded.ResourceTypeId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(seeded.ResourceTypeId, result.Id);
            Assert.Equal(name, result.Name);
            Assert.Equal(5, result.DefaultStaff);
        }

        [Fact]
        public async Task UpdateResourceType_UpdatesProperties()
        {
            // Arrange
            var originalName = UniqueName("Original");
            using var seedContext = _fixture.CreateContext();
            var seeded = await SeedResourceType(seedContext, name: originalName, defaultStaff: 2);

            var updatedName = UniqueName("Updated");
            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            var request = new ResourceTypeRequest
            {
                Name = updatedName,
                DefaultStaff = 7,
                NotificationMessage = "Updated notification",
            };

            // Act
            var result = await service.UpdateResourceType(seeded.ResourceTypeId, request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(updatedName, result.Name);
            Assert.Equal(7, result.DefaultStaff);

            // Verify in DB
            using var verifyContext = _fixture.CreateContext();
            var dbEntity = await verifyContext.ResourceTypes
                .AsNoTracking()
                .SingleAsync(r => r.ResourceTypeId == seeded.ResourceTypeId);
            Assert.Equal(updatedName, dbEntity.Name);
            Assert.Equal(7, dbEntity.DefaultStaff);
            Assert.Equal("Updated notification", dbEntity.NotificationMessage);
        }

        [Fact]
        public async Task UpdateResourceType_CanAddAndRemoveTrainers()
        {
            // Arrange - create users and resource type with one trainer
            using var seedContext = _fixture.CreateContext();
            var user1 = await SeedUser(seedContext, "Trainer", "One");
            var user2 = await SeedUser(seedContext, "Trainer", "Two");
            var seeded = await SeedResourceType(seedContext, name: UniqueName("Trainers"), trainerUserIds: new List<int> { user1.UserId });

            // Get the trainer ID that was created
            var existingTrainer = await seedContext.ResourceTypeTrainers
                .AsNoTracking()
                .SingleAsync(t => t.ResourceTypeId == seeded.ResourceTypeId && t.UserId == user1.UserId);

            // Act - remove user1, add user2
            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            var request = new ResourceTypeRequest
            {
                Name = seeded.Name,
                DefaultStaff = seeded.DefaultStaff,
                Trainers = new List<ResourceTypeTrainerRequest>
                {
                    new ResourceTypeTrainerRequest { Id = existingTrainer.ResourceTypeTrainerId, UserId = user1.UserId, IsDeleted = true },
                    new ResourceTypeTrainerRequest { Id = 0, UserId = user2.UserId, IsDeleted = false },
                },
            };

            var result = await service.UpdateResourceType(seeded.ResourceTypeId, request);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result.Trainers);
            Assert.Equal(user2.UserId, result.Trainers.First().UserId);

            // Verify in DB
            using var verifyContext = _fixture.CreateContext();
            var trainers = await verifyContext.ResourceTypeTrainers
                .AsNoTracking()
                .Where(t => t.ResourceTypeId == seeded.ResourceTypeId)
                .ToListAsync();
            Assert.Single(trainers);
            Assert.Equal(user2.UserId, trainers.First().UserId);
        }

        [Fact]
        public async Task DeleteResourceType_SetsInactive()
        {
            // Arrange
            var name = UniqueName("ToDelete");
            using var seedContext = _fixture.CreateContext();
            var seeded = await SeedResourceType(seedContext, name: name);

            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act
            var result = await service.DeleteResourceType(seeded.ResourceTypeId);

            // Assert
            Assert.NotNull(result);

            // Verify still in DB but marked inactive
            using var verifyContext = _fixture.CreateContext();
            var dbEntity = await verifyContext.ResourceTypes
                .AsNoTracking()
                .SingleAsync(r => r.ResourceTypeId == seeded.ResourceTypeId);
            Assert.True(dbEntity.Inactive);
            Assert.Equal(name, dbEntity.Name);
        }

        #endregion

        #region Training

        [Fact]
        public async Task UpdateTraining_ThrowsEntityNotFound_WhenTrainingDoesNotExist()
        {
            // Arrange
            using var context = _fixture.CreateContext();
            var service = CreateService(context, userId: 1);

            var request = new TrainingRequest { Id = 999999, ResourceTypeId = 1, UserId = 1, TrainingCompleted = true };

            // Act & Assert
            await Assert.ThrowsAsync<EntityNotFoundException>(() => service.UpdateTraining(1, request));
        }

        [Fact]
        public async Task CreateTraining_PersistsTrainingRecord()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var user = await SeedUser(seedContext, "Trainee", "Person");
            var confirmer = await SeedUser(seedContext, "Confirmer", "Person");
            // Bekrefteren er trener for ressurstypen og kan derfor sette opplæring for andre.
            var resourceType = await SeedResourceType(seedContext, name: UniqueName("Training"), trainerUserIds: new List<int> { confirmer.UserId });

            using var context = _fixture.CreateContext();
            var service = CreateService(context, userId: confirmer.UserId);

            var request = new TrainingRequest
            {
                UserId = user.UserId,
                ResourceTypeId = resourceType.ResourceTypeId,
                TrainingCompleted = true,
                StartTime = DateTime.UtcNow.AddDays(1),
            };

            // Act
            var result = await service.CreateTraining(resourceType.ResourceTypeId, request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(resourceType.ResourceTypeId, result.ResourceTypeId);
            Assert.True(result.TrainingComplete);
            Assert.NotNull(result.Confirmed);

            // Verify in DB
            using var verifyContext = _fixture.CreateContext();
            var dbTraining = await verifyContext.ResourceTypeTrainings
                .AsNoTracking()
                .SingleOrDefaultAsync(t => t.UserId == user.UserId && t.ResourceTypeId == resourceType.ResourceTypeId);
            Assert.NotNull(dbTraining);
            Assert.True(dbTraining.TrainingComplete);
            Assert.Equal(confirmer.UserId, dbTraining.ConfirmedBy);
        }

        [Fact]
        public async Task CreateTraining_SendsSmsToTrainers_WhenTrainingNotCompleted()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var trainee = await SeedUser(seedContext, "Trainee", "Sms");
            var trainer = await SeedUser(seedContext, "Trainer", "Sms");
            var resourceType = await SeedResourceType(seedContext,
                name: UniqueName("SmsTrain"),
                trainerUserIds: new List<int> { trainer.UserId });

            _smsSender.SendMessages(Arg.Any<IEnumerable<SmsMessage>>())
                .Returns(new SmsResult { Success = true });

            using var context = _fixture.CreateContext();
            var service = CreateService(context, userId: trainee.UserId);

            var request = new TrainingRequest
            {
                UserId = trainee.UserId,
                ResourceTypeId = resourceType.ResourceTypeId,
                TrainingCompleted = false,
                StartTime = DateTime.UtcNow.AddDays(3),
            };

            // Act
            var result = await service.CreateTraining(resourceType.ResourceTypeId, request);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.TrainingComplete);
            await _smsSender.Received(1).SendMessages(Arg.Is<IEnumerable<SmsMessage>>(msgs =>
                msgs.Any()));
        }

        #endregion

        #region NotFound

        [Fact]
        public async Task GetResourceTypeById_ThrowsEntityNotFound_WhenNotFound()
        {
            // Arrange
            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act & Assert
            await Assert.ThrowsAsync<EntityNotFoundException>(() => service.GetResourceTypeById(999999));
        }

        [Fact]
        public async Task UpdateResourceType_ThrowsEntityNotFound_WhenNotFound()
        {
            // Arrange
            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act & Assert
            await Assert.ThrowsAsync<EntityNotFoundException>(() => service.UpdateResourceType(999999, new ResourceTypeRequest { Name = "X" }));
        }

        [Fact]
        public async Task DeleteResourceType_ThrowsEntityNotFound_WhenNotFound()
        {
            // Arrange
            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act & Assert
            await Assert.ThrowsAsync<EntityNotFoundException>(() => service.DeleteResourceType(999999));
        }

        [Fact]
        public async Task GetFile_ThrowsEntityNotFound_WhenNotFound()
        {
            // Arrange
            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act & Assert
            await Assert.ThrowsAsync<EntityNotFoundException>(() => service.GetFile(999999, 999999));
        }

        [Fact]
        public async Task DeleteFile_ThrowsEntityNotFound_WhenNotFound()
        {
            // Arrange
            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act & Assert
            await Assert.ThrowsAsync<EntityNotFoundException>(() => service.DeleteFile(999999, 999999));
        }

        #endregion
        #region Tilgang til opplæring (#94)

        private async Task<ResourceTypeTraining> SeedTraining(PlannerDbContext context, int resourceTypeId, int userId, bool? completed = false)
        {
            var training = new ResourceTypeTraining { ResourceTypeId = resourceTypeId, UserId = userId, TrainingComplete = completed };
            context.ResourceTypeTrainings.Add(training);
            await context.SaveChangesAsync();
            return training;
        }

        [Fact]
        public async Task CreateTraining_AllowsUser_ToMarkOwnTrainingCompleted()
        {
            using var seedContext = _fixture.CreateContext();
            var user = await SeedUser(seedContext, "Self", "Declared");
            var resourceType = await SeedResourceType(seedContext);

            using var context = _fixture.CreateContext();
            var service = CreateService(context, userId: user.UserId);

            var request = new TrainingRequest { UserId = user.UserId, ResourceTypeId = resourceType.ResourceTypeId, TrainingCompleted = true };

            var result = await service.CreateTraining(resourceType.ResourceTypeId, request);

            Assert.NotNull(result);
            Assert.True(result.TrainingComplete);
            Assert.Equal(user.UserId, result.ConfirmedById);
        }

        [Fact]
        public async Task CreateTraining_ThrowsForbidden_WhenUserSetsTrainingForOtherUser()
        {
            using var seedContext = _fixture.CreateContext();
            var user = await SeedUser(seedContext, "Regular");
            var otherUser = await SeedUser(seedContext, "Other");
            var resourceType = await SeedResourceType(seedContext);

            using var context = _fixture.CreateContext();
            var service = CreateService(context, userId: user.UserId);

            // Både fullført og ikke fullført er avvist for andre brukere.
            await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.CreateTraining(resourceType.ResourceTypeId,
                new TrainingRequest { UserId = otherUser.UserId, ResourceTypeId = resourceType.ResourceTypeId, TrainingCompleted = true }));
            await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.CreateTraining(resourceType.ResourceTypeId,
                new TrainingRequest { UserId = otherUser.UserId, ResourceTypeId = resourceType.ResourceTypeId, TrainingCompleted = false }));

            using var verifyContext = _fixture.CreateContext();
            Assert.False(await verifyContext.ResourceTypeTrainings.AnyAsync(t => t.UserId == otherUser.UserId && t.ResourceTypeId == resourceType.ResourceTypeId));
        }

        [Fact]
        public async Task CreateTraining_ThrowsForbidden_WhenTrainerForOtherResourceType()
        {
            using var seedContext = _fixture.CreateContext();
            var trainer = await SeedUser(seedContext, "Trainer");
            var trainee = await SeedUser(seedContext, "Trainee");
            var resourceType = await SeedResourceType(seedContext);
            await SeedResourceType(seedContext, trainerUserIds: new List<int> { trainer.UserId });

            using var context = _fixture.CreateContext();
            var service = CreateService(context, userId: trainer.UserId);

            await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.CreateTraining(resourceType.ResourceTypeId,
                new TrainingRequest { UserId = trainee.UserId, ResourceTypeId = resourceType.ResourceTypeId, TrainingCompleted = true }));
        }

        [Fact]
        public async Task CreateTraining_AllowsAdmin_ToSetTrainingForOtherUser()
        {
            using var seedContext = _fixture.CreateContext();
            var admin = await SeedUser(seedContext, "Admin");
            var trainee = await SeedUser(seedContext, "Trainee");
            var resourceType = await SeedResourceType(seedContext);

            using var context = _fixture.CreateContext();
            var service = CreateService(context, userId: admin.UserId, isAdmin: true);

            var result = await service.CreateTraining(resourceType.ResourceTypeId,
                new TrainingRequest { UserId = trainee.UserId, ResourceTypeId = resourceType.ResourceTypeId, TrainingCompleted = true });

            Assert.NotNull(result);
            Assert.Equal(admin.UserId, result.ConfirmedById);
        }

        [Fact]
        public async Task UpdateTraining_AllowsTrainer_ToCompleteOtherUsersTraining()
        {
            using var seedContext = _fixture.CreateContext();
            var trainer = await SeedUser(seedContext, "Trainer");
            var trainee = await SeedUser(seedContext, "Trainee");
            var resourceType = await SeedResourceType(seedContext, trainerUserIds: new List<int> { trainer.UserId });
            var training = await SeedTraining(seedContext, resourceType.ResourceTypeId, trainee.UserId);

            using var context = _fixture.CreateContext();
            var service = CreateService(context, userId: trainer.UserId);

            var result = await service.UpdateTraining(resourceType.ResourceTypeId, new TrainingRequest
            {
                Id = training.ResourceTypeTrainingId,
                UserId = trainee.UserId,
                ResourceTypeId = resourceType.ResourceTypeId,
                TrainingCompleted = true,
            });

            Assert.NotNull(result);
            using var verifyContext = _fixture.CreateContext();
            var dbTraining = await verifyContext.ResourceTypeTrainings.AsNoTracking().SingleAsync(t => t.ResourceTypeTrainingId == training.ResourceTypeTrainingId);
            Assert.True(dbTraining.TrainingComplete);
            Assert.Equal(trainer.UserId, dbTraining.ConfirmedBy);
        }

        [Fact]
        public async Task UpdateTraining_ThrowsForbidden_WhenTrainingBelongsToOtherUser_EvenIfRequestClaimsOwnUserId()
        {
            using var seedContext = _fixture.CreateContext();
            var user = await SeedUser(seedContext, "Regular");
            var otherUser = await SeedUser(seedContext, "Other");
            var resourceType = await SeedResourceType(seedContext);
            var training = await SeedTraining(seedContext, resourceType.ResourceTypeId, otherUser.UserId);

            using var context = _fixture.CreateContext();
            var service = CreateService(context, userId: user.UserId);

            await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.UpdateTraining(resourceType.ResourceTypeId, new TrainingRequest
            {
                Id = training.ResourceTypeTrainingId,
                UserId = user.UserId,
                ResourceTypeId = resourceType.ResourceTypeId,
                TrainingCompleted = true,
            }));

            using var verifyContext = _fixture.CreateContext();
            var dbTraining = await verifyContext.ResourceTypeTrainings.AsNoTracking().SingleAsync(t => t.ResourceTypeTrainingId == training.ResourceTypeTrainingId);
            Assert.False(dbTraining.TrainingComplete);
        }

        [Fact]
        public async Task UpdateTraining_ThrowsDomainValidation_WhenUserIdDoesNotMatchTraining()
        {
            using var seedContext = _fixture.CreateContext();
            var trainer = await SeedUser(seedContext, "Trainer");
            var trainee = await SeedUser(seedContext, "Trainee");
            var otherUser = await SeedUser(seedContext, "Other");
            var resourceType = await SeedResourceType(seedContext, trainerUserIds: new List<int> { trainer.UserId });
            var training = await SeedTraining(seedContext, resourceType.ResourceTypeId, trainee.UserId);

            using var context = _fixture.CreateContext();
            var service = CreateService(context, userId: trainer.UserId);

            await Assert.ThrowsAsync<DomainValidationException>(() => service.UpdateTraining(resourceType.ResourceTypeId, new TrainingRequest
            {
                Id = training.ResourceTypeTrainingId,
                UserId = otherUser.UserId,
                ResourceTypeId = resourceType.ResourceTypeId,
                TrainingCompleted = true,
            }));
        }

        [Fact]
        public async Task UpdateTraining_ThrowsDomainValidation_WhenResourceTypeIdDoesNotMatchTraining()
        {
            using var seedContext = _fixture.CreateContext();
            var trainee = await SeedUser(seedContext, "Trainee");
            var resourceType = await SeedResourceType(seedContext);
            var otherResourceType = await SeedResourceType(seedContext);
            var training = await SeedTraining(seedContext, resourceType.ResourceTypeId, trainee.UserId);

            using var context = _fixture.CreateContext();
            var service = CreateService(context, userId: trainee.UserId);

            await Assert.ThrowsAsync<DomainValidationException>(() => service.UpdateTraining(otherResourceType.ResourceTypeId, new TrainingRequest
            {
                Id = training.ResourceTypeTrainingId,
                UserId = trainee.UserId,
                ResourceTypeId = otherResourceType.ResourceTypeId,
                TrainingCompleted = true,
            }));
        }

        #endregion

    }
}
