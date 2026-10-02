using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Middagsasen.Planner.Api.Core.OpenApi;
using Middagsasen.Planner.Api.Services.Events;

namespace Middagsasen.Planner.Api.Tests.Core.OpenApi
{
    public class NonNullableRequiredSchemaTransformerTests
    {
        private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };

        [Fact]
        public async Task TransformAsync_TypeOnTopLevel_MarksNonNullablePropertiesAsRequired()
        {
            var schema = CreateShiftUserSchema();

            await Transform(schema, Options.GetTypeInfo(typeof(ShiftUserResponse)), propertyInfo: null);

            AssertShiftUserRequired(schema);
        }

        [Fact]
        public async Task TransformAsync_TypeUsedAsProperty_MarksNonNullablePropertiesAsRequired()
        {
            // I egenskaps-kontekst (ShiftResponse.User) er JsonTypeInfo egenskapens type, og det er dette
            // schemaet som havner i components.schemas når typen ikke brukes på toppnivå.
            var schema = CreateShiftUserSchema();
            var userProperty = Options.GetTypeInfo(typeof(ShiftResponse)).Properties.Single(p => p.Name == "user");

            await Transform(schema, Options.GetTypeInfo(userProperty.PropertyType), userProperty);

            AssertShiftUserRequired(schema);
        }

        [Fact]
        public async Task TransformAsync_PrimitiveProperty_DoesNothing()
        {
            var schema = new OpenApiSchema { Type = JsonSchemaType.Integer };
            var idProperty = Options.GetTypeInfo(typeof(ShiftUserResponse)).Properties.Single(p => p.Name == "id");

            await Transform(schema, Options.GetTypeInfo(typeof(int)), idProperty);

            Assert.Null(schema.Required);
        }

        private static OpenApiSchema CreateShiftUserSchema() => new()
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["id"] = new OpenApiSchema { Type = JsonSchemaType.Integer },
                ["phoneNumber"] = new OpenApiSchema { Type = JsonSchemaType.String },
                ["firstName"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null },
                ["lastName"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null },
                ["fullName"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null },
                ["trainings"] = new OpenApiSchema { Type = JsonSchemaType.Array },
            },
        };

        private static void AssertShiftUserRequired(OpenApiSchema schema)
        {
            Assert.NotNull(schema.Required);
            Assert.Equal(new[] { "id", "phoneNumber", "trainings" }, schema.Required.Order());
        }

        private static Task Transform(OpenApiSchema schema, JsonTypeInfo typeInfo, JsonPropertyInfo? propertyInfo)
        {
            var context = new OpenApiSchemaTransformerContext
            {
                DocumentName = "v1",
                JsonTypeInfo = typeInfo,
                JsonPropertyInfo = propertyInfo,
                ParameterDescription = null,
                ApplicationServices = new EmptyServiceProvider(),
            };

            return new NonNullableRequiredSchemaTransformer().TransformAsync(schema, context, CancellationToken.None);
        }

        private sealed class EmptyServiceProvider : IServiceProvider
        {
            public object? GetService(Type serviceType) => null;
        }
    }
}
