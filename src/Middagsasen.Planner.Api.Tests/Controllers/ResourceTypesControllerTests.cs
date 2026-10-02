using Microsoft.AspNetCore.Mvc;
using Middagsasen.Planner.Api.Controllers;
using Middagsasen.Planner.Api.Services;
using Middagsasen.Planner.Api.Services.Competencies;
using Middagsasen.Planner.Api.Services.ResourceTypes;
using NSubstitute;

namespace Middagsasen.Planner.Api.Tests.Controllers
{
    public class ResourceTypesControllerTests
    {
        private readonly IResourceTypesService _resourceTypesService = Substitute.For<IResourceTypesService>();
        private readonly ResourceTypesController _sut;

        public ResourceTypesControllerTests()
        {
            _sut = new ResourceTypesController(_resourceTypesService, Substitute.For<ICompetencyService>());
        }

        [Fact]
        public async Task CreateTraining_ThrowsDomainValidationException_WhenServiceIgnoresRequest()
        {
            // Servicen returnerer null når TrainingCompleted mangler.
            var request = new TrainingRequest { ResourceTypeId = 1, UserId = 1, TrainingCompleted = null };
            _resourceTypesService.CreateTraining(1, request).Returns((TrainingResponse?)null);

            var ex = await Assert.ThrowsAsync<DomainValidationException>(() => _sut.CreateTraining(1, request));

            Assert.Equal(ResourceTypesController.TrainingCompletedRequiredMessage, ex.Message);
        }

        [Fact]
        public async Task CreateTraining_ReturnsCreated_WhenTrainingIsCreated()
        {
            var request = new TrainingRequest { ResourceTypeId = 1, UserId = 1, TrainingCompleted = true };
            var training = new TrainingResponse { Id = 5, ResourceTypeId = 1, TrainingComplete = true };
            _resourceTypesService.CreateTraining(1, request).Returns(training);

            var result = await _sut.CreateTraining(1, request);

            var created = Assert.IsType<CreatedResult>(result);
            Assert.Same(training, created.Value);
        }
    }
}
