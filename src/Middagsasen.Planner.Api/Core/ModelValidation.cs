using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.AspNetCore.WebUtilities;
using Middagsasen.Planner.Api.Core.OpenApi;

namespace Middagsasen.Planner.Api.Core
{
    /// <summary>
    /// Norske 400-svar når modellbindingen eller -valideringen feiler (f.eks. en tid som ikke kan tolkes, eller et
    /// påkrevd felt som mangler i JSON). Svaret er <see cref="ValidationProblemDetails"/> med samme <c>type</c>,
    /// <c>title</c> og <c>traceId</c> som de andre feilsvarene (se <see cref="Authentication.ExceptionHandlingMiddleware"/>),
    /// en norsk <c>detail</c> og <c>errors</c> per felt med JSON-navn (camelCase, uten <c>$.</c>).
    /// </summary>
    internal static partial class ModelValidation
    {
        internal const string Detail = "Forespørselen inneholder ugyldige verdier.";
        internal const string InvalidValueMessage = "Ugyldig verdi.";
        internal const string MissingValueMessage = "Feltet mangler.";
        internal const string InvalidBodyMessage = "Ugyldig innhold i forespørselen.";
        internal const string MissingBodyMessage = "Forespørselen mangler innhold.";

        /// <summary>Feltnøkkelen for feil som gjelder hele forespørselen og ikke ett felt.</summary>
        internal const string RootKey = "";

        public static IMvcBuilder AddNorwegianModelValidation(this IMvcBuilder builder)
        {
            builder.Services.Configure<MvcOptions>(options =>
            {
                // Valideringsfeil (DataAnnotations) får JSON-navnet (camelCase) som nøkkel, som feilene fra JSON-tolkingen.
                options.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider());

                // Bindingsfeil utenfor JSON-body (rute, query, skjema).
                var messages = options.ModelBindingMessageProvider;
                messages.SetAttemptedValueIsInvalidAccessor((_, _) => InvalidValueMessage);
                messages.SetNonPropertyAttemptedValueIsInvalidAccessor(_ => InvalidValueMessage);
                messages.SetUnknownValueIsInvalidAccessor(_ => InvalidValueMessage);
                messages.SetNonPropertyUnknownValueIsInvalidAccessor(() => InvalidValueMessage);
                messages.SetValueIsInvalidAccessor(_ => InvalidValueMessage);
                messages.SetValueMustBeANumberAccessor(_ => InvalidValueMessage);
                messages.SetNonPropertyValueMustBeANumberAccessor(() => InvalidValueMessage);
                messages.SetValueMustNotBeNullAccessor(_ => MissingValueMessage);
                messages.SetMissingBindRequiredValueAccessor(_ => MissingValueMessage);
                messages.SetMissingKeyOrValueAccessor(() => MissingValueMessage);
                messages.SetMissingRequestBodyRequiredValueAccessor(() => MissingBodyMessage);
            });
            builder.ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = CreateResponse);
            return builder;
        }

        internal static IActionResult CreateResponse(ActionContext context)
        {
            var bodyParameters = context.ActionDescriptor.Parameters
                .Where(p => p.BindingInfo?.BindingSource == BindingSource.Body)
                .Select(p => p.BindingInfo?.BinderModelName ?? p.Name)
                .ToHashSet();
            var errors = Translate(context.ModelState, bodyParameters);
            var problemDetails = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>()
                .CreateValidationProblemDetails(
                    context.HttpContext,
                    errors,
                    StatusCodes.Status400BadRequest,
                    title: ReasonPhrases.GetReasonPhrase(StatusCodes.Status400BadRequest),
                    detail: Detail);

            return new BadRequestObjectResult(problemDetails)
            {
                ContentTypes = { ProblemDetailsContentTypeConvention.ProblemJson },
            };
        }

        /// <summary>
        /// Oversetter feilene. Feil fra JSON-tolkingen har nøkkel som JSON-sti (<c>$</c>, <c>$.startTime</c>,
        /// <c>$.resources[0].startTime</c>) og engelsk melding fra System.Text.Json: de får feltnavn uten <c>$.</c> og
        /// norsk melding. Manglende påkrevde egenskaper meldes av System.Text.Json på objektet, og fordeles her
        /// på hvert felt. Når bodyen ikke kunne tolkes, melder MVC i tillegg at hele body-parameteren mangler
        /// («The request field is required.»): den feilen er da overflødig og tas bort, og uten andre feil blir den
        /// <see cref="MissingBodyMessage"/>. Øvrige feil (DataAnnotations og bindingsfeil) beholdes som de er.
        /// </summary>
        internal static ModelStateDictionary Translate(ModelStateDictionary modelState, IReadOnlySet<string> bodyParameters)
        {
            var hasJsonErrors = modelState.Any(e => IsJsonPath(e.Key) && e.Value?.Errors.Count > 0);
            var result = new ModelStateDictionary();
            foreach (var (key, entry) in modelState)
            {
                foreach (var error in entry.Errors)
                {
                    var message = string.IsNullOrEmpty(error.ErrorMessage) ? error.Exception?.Message ?? string.Empty : error.ErrorMessage;
                    if (bodyParameters.Contains(key))
                    {
                        if (!hasJsonErrors)
                            Add(result, RootKey, MissingBodyMessage);
                        continue;
                    }
                    if (!IsJsonPath(key))
                    {
                        Add(result, key, message);
                        continue;
                    }

                    var field = FieldName(key);
                    var missing = MissingProperties(message);
                    if (missing.Count > 0)
                    {
                        foreach (var property in missing)
                            Add(result, field.Length == 0 ? property : $"{field}.{property}", MissingValueMessage);
                    }
                    else
                    {
                        Add(result, field, field.Length == 0 ? InvalidBodyMessage : InvalidValueMessage);
                    }
                }
            }
            return result;
        }

        private static void Add(ModelStateDictionary result, string key, string message)
        {
            if (result.TryGetValue(key, out var entry) && entry.Errors.Any(e => e.ErrorMessage == message))
                return;
            result.TryAddModelError(key, message);
        }

        private static bool IsJsonPath(string key) => key == "$" || key.StartsWith("$.") || key.StartsWith("$[");

        private static string FieldName(string jsonPath) =>
            jsonPath.StartsWith("$.") ? jsonPath[2..] : jsonPath[1..];

        /// <summary>
        /// Egenskapene System.Text.Json melder som manglende («… was missing required properties including: 'a', 'b'.»).
        /// Listen kan være avkortet av System.Text.Json når mange mangler.
        /// </summary>
        private static List<string> MissingProperties(string message)
        {
            var match = MissingPropertiesRegex().Match(message);
            return match.Success
                ? QuotedNameRegex().Matches(match.Groups[1].Value).Select(m => m.Groups[1].Value).ToList()
                : [];
        }

        [GeneratedRegex(@"missing required properties[^:]*:\s*(.*)$", RegexOptions.Singleline)]
        private static partial Regex MissingPropertiesRegex();

        [GeneratedRegex(@"'([^']+)'")]
        private static partial Regex QuotedNameRegex();
    }
}
