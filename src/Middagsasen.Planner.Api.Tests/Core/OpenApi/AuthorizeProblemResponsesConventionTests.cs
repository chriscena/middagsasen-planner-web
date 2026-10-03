using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Formatters;
using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Core.OpenApi;

namespace Middagsasen.Planner.Api.Tests.Core.OpenApi
{
    public class AuthorizeProblemResponsesConventionTests
    {
        private sealed class OpenController
        {
            public void Anonymous() { }

            [Authorize]
            public void LoggedIn() { }

            [Authorize(Role = Roles.Administrator)]
            public void AdminOnly() { }

            [Authorize(Role = Roles.Administrator)]
            [ProducesResponseType(typeof(string), StatusCodes.Status403Forbidden)]
            public void AdminOnlyAlreadyDocumented() { }
        }

        [Authorize]
        private sealed class ProtectedController
        {
            public void Inherited() { }
        }

        private static ActionModel Apply<TController>(string actionName)
        {
            var controllerType = typeof(TController).GetTypeInfo();
            var controller = new ControllerModel(controllerType, controllerType.GetCustomAttributes(inherit: true));
            foreach (var filter in controller.Attributes.OfType<Microsoft.AspNetCore.Mvc.Filters.IFilterMetadata>())
                controller.Filters.Add(filter);

            var method = controllerType.GetMethod(actionName)!;
            var action = new ActionModel(method, method.GetCustomAttributes(inherit: true)) { Controller = controller };
            foreach (var filter in action.Attributes.OfType<Microsoft.AspNetCore.Mvc.Filters.IFilterMetadata>())
                action.Filters.Add(filter);

            new AuthorizeProblemResponsesConvention().Apply(action);
            return action;
        }

        private static IApiResponseMetadataProvider[] Responses(ActionModel action, int statusCode)
            => action.Filters.OfType<IApiResponseMetadataProvider>().Where(p => p.StatusCode == statusCode).ToArray();

        private static string[] ContentTypes(IApiResponseMetadataProvider provider)
        {
            var contentTypes = new MediaTypeCollection();
            provider.SetContentTypes(contentTypes);
            return contentTypes.ToArray();
        }

        [Fact]
        public void DoesNothing_WithoutAuthorize()
        {
            var action = Apply<OpenController>(nameof(OpenController.Anonymous));

            Assert.Empty(action.Filters.OfType<IApiResponseMetadataProvider>());
        }

        [Fact]
        public void Adds401ProblemDetails_WhenLoginRequired()
        {
            var action = Apply<OpenController>(nameof(OpenController.LoggedIn));

            var response = Assert.Single(Responses(action, StatusCodes.Status401Unauthorized));
            Assert.Equal(typeof(ProblemDetails), ((ProducesResponseTypeAttribute)response).Type);
            Assert.Equal(["application/problem+json"], ContentTypes(response));
            Assert.Empty(Responses(action, StatusCodes.Status403Forbidden));
        }

        [Fact]
        public void Adds401And403ProblemDetails_WhenRoleRequired()
        {
            var action = Apply<OpenController>(nameof(OpenController.AdminOnly));

            Assert.Single(Responses(action, StatusCodes.Status401Unauthorized));
            var forbidden = Assert.Single(Responses(action, StatusCodes.Status403Forbidden));
            Assert.Equal(typeof(ProblemDetails), ((ProducesResponseTypeAttribute)forbidden).Type);
            Assert.Equal(["application/problem+json"], ContentTypes(forbidden));
        }

        [Fact]
        public void KeepsExistingAnnotation()
        {
            var action = Apply<OpenController>(nameof(OpenController.AdminOnlyAlreadyDocumented));

            var forbidden = Assert.Single(Responses(action, StatusCodes.Status403Forbidden));
            Assert.Equal(typeof(string), ((ProducesResponseTypeAttribute)forbidden).Type);
        }

        [Fact]
        public void UsesAuthorizeOnController()
        {
            var action = Apply<ProtectedController>(nameof(ProtectedController.Inherited));

            Assert.Single(Responses(action, StatusCodes.Status401Unauthorized));
        }
    }
}
