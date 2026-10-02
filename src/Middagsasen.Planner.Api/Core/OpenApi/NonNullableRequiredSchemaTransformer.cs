using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Middagsasen.Planner.Api.Core.OpenApi
{
    /// <summary>
    /// Markerer alle egenskaper som ikke kan være null som <c>required</c> i OpenAPI-schemaet.
    ///
    /// Den innebygde generatoren i .NET 10 respekterer nullable reference types (en <c>string</c>
    /// uten <c>?</c> får ikke <c>null</c> i typen), men legger bare egenskaper med <c>required</c>-modifier
    /// eller <c>[Required]</c> i <c>required</c>-listen. Uten dette blir alle felt valgfrie
    /// (<c>name?: string</c>) i TypeScript-typene som genereres med openapi-typescript.
    ///
    /// Nullability leses fra <see cref="JsonPropertyInfo.IsGetNullable"/>, som System.Text.Json fyller ut
    /// fra NRT-annotasjonene (referansetyper) og fra <c>Nullable&lt;T&gt;</c> (verdityper). Vi ser på
    /// getteren fordi det er den som bestemmer hva som faktisk serialiseres i responsen.
    /// </summary>
    internal sealed class NonNullableRequiredSchemaTransformer : IOpenApiSchemaTransformer
    {
        public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
        {
            // Transformeren kalles også for hver enkelt egenskap; vi vil bare behandle selve objekt-schemaet.
            if (context.JsonPropertyInfo is not null || context.JsonTypeInfo.Kind != JsonTypeInfoKind.Object || schema.Properties is null)
            {
                return Task.CompletedTask;
            }

            foreach (var property in context.JsonTypeInfo.Properties)
            {
                // Hopp over egenskaper som ikke kan leses (ikke en del av responsen) og de som kan være null.
                if (property.Get is null || property.IsGetNullable)
                {
                    continue;
                }

                // property.Name er det serialiserte navnet (camelCase), samme nøkkel som i schema.Properties.
                if (schema.Properties.ContainsKey(property.Name))
                {
                    schema.Required ??= new HashSet<string>();
                    schema.Required.Add(property.Name);
                }
            }

            return Task.CompletedTask;
        }
    }
}
