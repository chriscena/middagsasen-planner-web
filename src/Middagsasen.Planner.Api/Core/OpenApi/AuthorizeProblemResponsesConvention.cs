using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Middagsasen.Planner.Api.Authentication;

namespace Middagsasen.Planner.Api.Core.OpenApi
{
    /// <summary>
    /// Dokumenterer svarene fra <see cref="AuthorizeAttribute"/> i OpenAPI-dokumentet: 401 (ProblemDetails) på alle
    /// endepunkter som krever innlogging, og 403 (ProblemDetails) på endepunkter som krever en rolle,
    /// som <c>application/problem+json</c>.
    /// Legges til som <see cref="ProducesResponseTypeAttribute"/> på actionen, slik at svarene beskrives likt
    /// som de som annoteres for hånd. Statuskoder som allerede er annotert på actionen eller controlleren, beholdes.
    /// </summary>
    internal sealed class AuthorizeProblemResponsesConvention : IActionModelConvention
    {
        public void Apply(ActionModel action)
        {
            var authorizeAttributes = action.Attributes
                .Concat(action.Controller.Attributes)
                .OfType<AuthorizeAttribute>()
                .ToList();
            if (authorizeAttributes.Count == 0) return;

            AddIfMissing(action, StatusCodes.Status401Unauthorized);

            if (authorizeAttributes.Any(a => !string.IsNullOrWhiteSpace(a.Role)))
                AddIfMissing(action, StatusCodes.Status403Forbidden);
        }

        private static void AddIfMissing(ActionModel action, int statusCode)
        {
            var documented = action.Filters
                .Concat(action.Controller.Filters)
                .OfType<IApiResponseMetadataProvider>()
                .Any(p => p.StatusCode == statusCode);
            if (documented) return;

            action.Filters.Add(new ProducesResponseTypeAttribute(
                typeof(ProblemDetails), statusCode, ProblemDetailsContentTypeConvention.ProblemJson));
        }
    }
}
