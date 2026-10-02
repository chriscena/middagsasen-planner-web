using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Middagsasen.Planner.Api.Core.OpenApi
{
    /// <summary>
    /// Legger til verdilisten for enum-typer i OpenAPI-schemaet.
    ///
    /// API-et serialiserer enums som heltall (ingen <c>JsonStringEnumConverter</c>). Da beskriver den
    /// innebygde generatoren dem bare som <c>{ "type": "integer" }</c>, uten hvilke verdier som er gyldige,
    /// og de blir <c>number</c> i TypeScript-typene. Vi setter <c>enum</c> til heltallsverdiene og legger
    /// navnene i <c>x-enum-varnames</c> (samme rekkefølge), som kodegeneratorer bruker til å lage navngitte
    /// konstanter. Selve serialiseringen endres ikke.
    ///
    /// For <c>Nullable&lt;TEnum&gt;</c> brukes den underliggende enum-typen. Hvis schemaet tillater <c>null</c>,
    /// tas <c>null</c> med i verdilisten så den ikke utelukker null.
    /// </summary>
    internal sealed class EnumSchemaTransformer : IOpenApiSchemaTransformer
    {
        public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
        {
            var type = context.JsonTypeInfo.Type;
            var enumType = Nullable.GetUnderlyingType(type) ?? type;
            if (!enumType.IsEnum)
            {
                return Task.CompletedTask;
            }

            var underlyingType = Enum.GetUnderlyingType(enumType);
            var names = Enum.GetNames(enumType);

            var values = new List<JsonNode>();
            var varNames = new JsonArray();
            foreach (var name in names)
            {
                var value = Convert.ChangeType(Enum.Parse(enumType, name), underlyingType);
                values.Add(JsonValue.Create(Convert.ToInt64(value))!);
                varNames.Add(name);
            }

            if (schema.Type is { } schemaType && schemaType.HasFlag(JsonSchemaType.Null))
            {
                // JsonNode-representasjonen av JSON null er en null-referanse.
                values.Add(null!);
            }

            schema.Enum = values;
            schema.Extensions ??= new Dictionary<string, IOpenApiExtension>();
            schema.Extensions["x-enum-varnames"] = new JsonNodeExtension(varNames);

            return Task.CompletedTask;
        }
    }
}
