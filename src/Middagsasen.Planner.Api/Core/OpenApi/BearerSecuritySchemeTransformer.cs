using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Middagsasen.Planner.Api.Core.OpenApi
{
    /// <summary>
    /// Legger til et <c>Bearer</c> http security scheme i OpenAPI-dokumentet, slik at man kan lime inn
    /// JWT-token via "Authorize"-knappen i Swagger UI. Kravet legges globalt på dokumentet fordi
    /// autentisering håndteres av egen JwtMiddleware og [Authorize]-attributt, som generatoren ikke kjenner til.
    /// </summary>
    internal sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
    {
        private const string SchemeName = "Bearer";

        public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
        {
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "JWT-token fra /api/authentication/authenticate. Lim inn kun selve tokenet (uten \"Bearer \")."
            };

            document.Security ??= new List<OpenApiSecurityRequirement>();
            document.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(SchemeName, document)] = new List<string>()
            });

            return Task.CompletedTask;
        }
    }
}
