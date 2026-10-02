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
    ///
    /// Transformeren kalles både for typer på toppnivå og for hver egenskap i en annen type. I egenskaps-kontekst
    /// er <see cref="OpenApiSchemaTransformerContext.JsonTypeInfo"/> egenskapens <em>egen</em> type (ikke den
    /// omsluttende), og <paramref name="schema"/> er hele schemaet for den typen. En DTO som bare brukes som
    /// egenskap i en annen DTO (f.eks. <c>ShiftUserResponse</c> via <c>ShiftResponse.User</c>) behandles
    /// kun i denne konteksten, og det er det schemaet som havner i <c>components.schemas</c>. Vi må derfor ikke
    /// hoppe over kall der <see cref="OpenApiSchemaTransformerContext.JsonPropertyInfo"/> er satt, men alltid
    /// basere oss på <c>JsonTypeInfo</c>. Primitive egenskaper har <c>Kind == None</c> og ingen properties,
    /// og påvirkes ikke. For en nullable objekt-egenskap pakker generatoren inn schemaet i
    /// <c>oneOf: [{type: null}, {$ref}]</c> etter at transformeren har kjørt; <c>required</c> havner da på det
    /// refererte objekt-schemaet, ikke på selve egenskapen, så egenskapen er fortsatt valgfri/nullable.
    /// </summary>
    internal sealed class NonNullableRequiredSchemaTransformer : IOpenApiSchemaTransformer
    {
        public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
        {
            // JsonTypeInfo er typen schemaet beskriver, også når transformeren kalles for en egenskap.
            if (context.JsonTypeInfo.Kind != JsonTypeInfoKind.Object || schema.Properties is null)
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
