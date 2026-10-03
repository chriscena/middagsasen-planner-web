using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Middagsasen.Planner.Api.Authentication;
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

        private static AuthorizeAttribute? GetFileAuthorizeAttribute()
            => typeof(ResourceTypesController).GetMethod(nameof(ResourceTypesController.GetFile))!
                .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>()
                .SingleOrDefault();

        [Fact]
        public void GetFile_HasAuthorizeAttribute()
        {
            var authorize = GetFileAuthorizeAttribute();

            Assert.NotNull(authorize);
            Assert.Null(authorize.Role);
        }

        [Fact]
        public void GetFile_AnonymousRequest_IsRejectedWithUnauthorized()
        {
            var actionContext = new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor());
            var context = new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());

            // Kaster i stedet for å sette context.Result, slik at svaret blir ProblemDetails (401).
            Assert.Throws<NotAuthenticatedException>(() => GetFileAuthorizeAttribute()!.OnAuthorization(context));
        }
    }
}
