using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Middagsasen.Planner.Api.Core.OpenApi
{
    /// <summary>
    /// Fjerner <c>string</c> som alternativ type for tall i OpenAPI-schemaet.
    ///
    /// ASP.NET Core bruker <c>JsonSerializerDefaults.Web</c>, der <c>NumberHandling = AllowReadingFromString</c>.
    /// Generatoren gjenspeiler dette ved å beskrive f.eks. en <c>int</c> som
    /// <c>type: [integer, string]</c> med et regex-<c>pattern</c>, som blir <c>number | string</c> i TypeScript.
    /// API-et skriver alltid tall som tall, så vi beskriver dem som rene tall i dokumentet.
    /// Selve serialiseringen endres ikke (API-et godtar fortsatt tall sendt som streng).
    /// </summary>
    internal sealed class StrictNumberSchemaTransformer : IOpenApiSchemaTransformer
    {
        private const JsonSchemaType NumericTypes = JsonSchemaType.Integer | JsonSchemaType.Number;

        public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
        {
            if (schema.Type is { } type
                && (type & NumericTypes) != 0
                && type.HasFlag(JsonSchemaType.String))
            {
                schema.Type = type & ~JsonSchemaType.String;
                // Pattern beskriver kun streng-varianten av tallet og er ikke lenger relevant.
                schema.Pattern = null;
            }

            return Task.CompletedTask;
        }
    }
}
