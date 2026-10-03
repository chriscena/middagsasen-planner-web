using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Formatters;

namespace Middagsasen.Planner.Api.Core.OpenApi
{
    /// <summary>
    /// Dokumenterer feilsvar med <see cref="ProblemDetails"/> som <c>application/problem+json</c>, som er det API-et
    /// faktisk svarer (se <see cref="Authentication.ExceptionHandlingMiddleware"/>). Uten content type beskrives
    /// <c>[ProducesResponseType(typeof(ProblemDetails), ...)]</c> med output-formaternes standardtyper
    /// (<c>text/plain</c>, <c>application/json</c>, <c>text/json</c>). Annoteringer som allerede har content type, beholdes.
    /// </summary>
    internal sealed class ProblemDetailsContentTypeConvention : IActionModelConvention
    {
        public const string ProblemJson = "application/problem+json";

        public void Apply(ActionModel action)
        {
            Normalize(action.Filters);
            Normalize(action.Controller.Filters);
        }

        private static void Normalize(IList<Microsoft.AspNetCore.Mvc.Filters.IFilterMetadata> filters)
        {
            for (var i = 0; i < filters.Count; i++)
            {
                if (filters[i] is not ProducesResponseTypeAttribute attribute
                    || attribute.GetType() != typeof(ProducesResponseTypeAttribute)
                    || !typeof(ProblemDetails).IsAssignableFrom(attribute.Type)
                    || HasContentTypes(attribute))
                    continue;

                filters[i] = new ProducesResponseTypeAttribute(attribute.Type, attribute.StatusCode, ProblemJson)
                {
                    Description = attribute.Description,
                };
            }
        }

        private static bool HasContentTypes(IApiResponseMetadataProvider provider)
        {
            var contentTypes = new MediaTypeCollection();
            provider.SetContentTypes(contentTypes);
            return contentTypes.Count > 0;
        }
    }
}
