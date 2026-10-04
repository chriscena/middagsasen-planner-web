using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Middagsasen.Planner.Api.Core.OpenApi
{
    /// <summary>
    /// Gir egenskaper med egen JSON-konverter (<see cref="LocalDateTimeConverter"/>, <see cref="LocalDateConverter"/>)
    /// <c>type: string</c> igjen. Generatoren vet ikke hva en egen konverter skriver, og dropper derfor typen, men
    /// beholder <c>format</c> (<c>date-time</c>/<c>date</c>). Uten typen blir feltet <c>unknown</c> i TypeScript.
    /// </summary>
    internal sealed class CustomConverterStringSchemaTransformer : IOpenApiSchemaTransformer
    {
        public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
        {
            if (schema.Type is null
                && context.JsonPropertyInfo?.CustomConverter is LocalDateTimeConverter or LocalDateConverter)
            {
                schema.Type = JsonSchemaType.String;
            }

            return Task.CompletedTask;
        }
    }
}
