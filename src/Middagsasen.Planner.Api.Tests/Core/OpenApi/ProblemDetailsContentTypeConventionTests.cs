using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Formatters;
using Middagsasen.Planner.Api.Core.OpenApi;

namespace Middagsasen.Planner.Api.Tests.Core.OpenApi
{
    public class ProblemDetailsContentTypeConventionTests
    {
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        private sealed class TestController
        {
            [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
            [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, Description = "Fant ikke")]
            [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
            [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/json")]
            public void Action() { }
        }

        private static ActionModel Apply()
        {
            var controllerType = typeof(TestController).GetTypeInfo();
            var controller = new ControllerModel(controllerType, controllerType.GetCustomAttributes(inherit: true));
            foreach (var filter in controller.Attributes.OfType<IFilterMetadata>())
                controller.Filters.Add(filter);

            var method = controllerType.GetMethod(nameof(TestController.Action))!;
            var action = new ActionModel(method, method.GetCustomAttributes(inherit: true)) { Controller = controller };
            foreach (var filter in action.Attributes.OfType<IFilterMetadata>())
                action.Filters.Add(filter);

            new ProblemDetailsContentTypeConvention().Apply(action);
            return action;
        }

        private static ProducesResponseTypeAttribute Response(ActionModel action, int statusCode)
            => action.Filters.Concat(action.Controller.Filters)
                .OfType<ProducesResponseTypeAttribute>()
                .Single(p => p.StatusCode == statusCode);

        private static string[] ContentTypes(IApiResponseMetadataProvider provider)
        {
            var contentTypes = new MediaTypeCollection();
            provider.SetContentTypes(contentTypes);
            return contentTypes.ToArray();
        }

        [Fact]
        public void SetsProblemJson_OnProblemDetailsResponses()
        {
            var action = Apply();

            var notFound = Response(action, StatusCodes.Status404NotFound);
            Assert.Equal(typeof(ProblemDetails), notFound.Type);
            Assert.Equal("Fant ikke", notFound.Description);
            Assert.Equal(["application/problem+json"], ContentTypes(notFound));
            Assert.Equal(["application/problem+json"], ContentTypes(Response(action, StatusCodes.Status400BadRequest)));
        }

        [Fact]
        public void SetsProblemJson_OnControllerAnnotations()
        {
            var action = Apply();

            Assert.Equal(["application/problem+json"], ContentTypes(Response(action, StatusCodes.Status500InternalServerError)));
        }

        [Fact]
        public void KeepsOtherResponses_AndExplicitContentTypes()
        {
            var action = Apply();

            Assert.Empty(ContentTypes(Response(action, StatusCodes.Status200OK)));
            Assert.Equal(["application/json"], ContentTypes(Response(action, StatusCodes.Status409Conflict)));
        }
    }
}
