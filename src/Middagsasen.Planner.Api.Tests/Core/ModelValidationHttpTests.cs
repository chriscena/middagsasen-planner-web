using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Controllers;
using Middagsasen.Planner.Api.Core;
using Middagsasen.Planner.Api.Services;
using Middagsasen.Planner.Api.Services.Events;
using NSubstitute;

namespace Middagsasen.Planner.Api.Tests.Core
{
    /// <summary>
    /// Modellbinding og -validering over HTTP: de ekte controllerne, JSON-oppsettet og <see cref="ModelValidation"/>
    /// i en minimal testserver, med servicene byttet ut. Viser hvilket wire-format tidsfeltene godtar, og at ugyldige
    /// eller manglende verdier gir norsk 400 før servicen kalles.
    /// </summary>
    public sealed class ModelValidationHttpTests : IAsyncLifetime
    {
        private readonly IEventsService _events = Substitute.For<IEventsService>();
        private readonly IEventTemplatesService _templates = Substitute.For<IEventTemplatesService>();
        private WebApplication _app = null!;
        private HttpClient _client = null!;

        public async Task InitializeAsync()
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddControllers()
                .AddApplicationPart(typeof(EventsController).Assembly)
                .AddNorwegianModelValidation();
            builder.Services.AddProblemDetails();
            builder.Services.AddSingleton(_events);
            builder.Services.AddSingleton(_templates);
            builder.Services.AddSingleton(Substitute.For<ICurrentUserService>());

            _app = builder.Build();
            _app.UseMiddleware<ExceptionHandlingMiddleware>();
            // Innlogget administrator, som JwtMiddleware ville satt.
            _app.Use((context, next) =>
            {
                context.Items["User"] = new Actor(1, IsAdmin: true);
                context.User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.Role, Roles.Administrator)], "Test", ClaimTypes.Name, ClaimTypes.Role));
                return next(context);
            });
            _app.MapControllers();
            await _app.StartAsync();
            _client = _app.GetTestClient();

            _events.CreateEvent(Arg.Any<EventRequest>()).Returns(new EventResponse());
            _events.UpdateEvent(Arg.Any<int>(), Arg.Any<EventRequest>()).Returns(new EventResponse());
            _events.CreateEventFromTemplate(Arg.Any<int>(), Arg.Any<EventFromTemplateRequest>()).Returns(new EventResponse());
            _templates.CreateEventTemplate(Arg.Any<EventTemplateRequest>()).Returns(new EventTemplateResponse());
            _templates.UpdateEventTemplate(Arg.Any<int>(), Arg.Any<EventTemplateRequest>()).Returns(new EventTemplateResponse());
        }

        public async Task DisposeAsync()
        {
            _client.Dispose();
            await _app.DisposeAsync();
        }

        private const string ValidEvent = """
            {
              "name": "Kveldsrenn",
              "startTime": "2026-01-15T22:00",
              "endTime": "2026-01-16T02:00",
              "resources": [ { "resourceTypeId": 1, "startTime": "23:00", "endTime": "01:30", "minimumStaff": 2 } ]
            }
            """;

        private const string ValidTemplate = """
            {
              "name": "Mal",
              "eventName": "Kveldsrenn",
              "startTime": "18:00",
              "endTime": "21:00",
              "resourceTemplates": [ { "resourceTypeId": 1, "startTime": "18:30", "endTime": "20:30", "minimumStaff": 1 } ]
            }
            """;

        private Task<HttpResponseMessage> Send(string method, string url, string json) =>
            _client.SendAsync(new HttpRequestMessage(new HttpMethod(method), url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });

        /// <summary>Bytter ut én verdi (eller fjerner egenskapen når <paramref name="value"/> er null) på stien.</summary>
        private static string With(string json, string path, string? value)
        {
            var root = System.Text.Json.Nodes.JsonNode.Parse(json)!;
            var segments = path.Split('.');
            System.Text.Json.Nodes.JsonNode node = root;
            foreach (var segment in segments[..^1])
            {
                var bracket = segment.IndexOf('[');
                node = bracket < 0
                    ? node[segment]!
                    : node[segment[..bracket]]![int.Parse(segment[(bracket + 1)..^1])]!;
            }
            var obj = node.AsObject();
            if (value is null)
                obj.Remove(segments[^1]);
            else
                obj[segments[^1]] = System.Text.Json.Nodes.JsonNode.Parse(value);
            return root.ToJsonString();
        }

        private static async Task<JsonElement> AssertValidationProblem(HttpResponseMessage response)
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
            Assert.Equal(400, body.GetProperty("status").GetInt32());
            Assert.Equal("Bad Request", body.GetProperty("title").GetString());
            Assert.Equal(ModelValidation.Detail, body.GetProperty("detail").GetString());
            Assert.StartsWith("https://tools.ietf.org/html/rfc9110#section-15.5.1", body.GetProperty("type").GetString());
            Assert.True(body.TryGetProperty("traceId", out _));
            return body.GetProperty("errors");
        }

        private static Dictionary<string, string[]> Errors(JsonElement errors) =>
            errors.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.EnumerateArray().Select(e => e.GetString()!).ToArray());

        #region Wire-format som godtas

        [Fact]
        public async Task CreateEvent_BindsLocalTimeWithoutSeconds_AndClockTimes()
        {
            var response = await Send("POST", "/api/events", ValidEvent);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            await _events.Received(1).CreateEvent(Arg.Is<EventRequest>(r =>
                r.StartTime == new DateTime(2026, 1, 15, 22, 0, 0) && r.StartTime.Kind == DateTimeKind.Unspecified
                && r.EndTime == new DateTime(2026, 1, 16, 2, 0, 0) && r.EndTime.Kind == DateTimeKind.Unspecified
                && r.Resources.Single().StartTime == new TimeOnly(23, 0)
                && r.Resources.Single().EndTime == new TimeOnly(1, 30)));
        }

        [Theory]
        [InlineData("\"2026-01-15T22:00:00\"", "\"23:00:00\"")]
        [InlineData("\"2026-01-15T22:00\"", "\"23:00\"")]
        public async Task CreateEvent_AcceptsOptionalSeconds(string startTime, string resourceStartTime)
        {
            var json = With(With(ValidEvent, "startTime", startTime), "resources[0].startTime", resourceStartTime);

            var response = await Send("POST", "/api/events", json);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            await _events.Received(1).CreateEvent(Arg.Is<EventRequest>(r =>
                r.StartTime == new DateTime(2026, 1, 15, 22, 0, 0) && r.Resources.Single().StartTime == new TimeOnly(23, 0)));
        }

        [Fact]
        public async Task CreateEventFromTemplate_BindsDayKey()
        {
            var response = await Send("POST", "/api/events/template/3", """{ "startDate": "2026-10-07" }""");

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            await _events.Received(1).CreateEventFromTemplate(3, Arg.Is<EventFromTemplateRequest>(r => r.StartDate == new DateOnly(2026, 10, 7)));
        }

        [Fact]
        public async Task CreateTemplate_BindsClockTimes()
        {
            var response = await Send("POST", "/api/templates", ValidTemplate);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            await _templates.Received(1).CreateEventTemplate(Arg.Is<EventTemplateRequest>(r =>
                r.StartTime == new TimeOnly(18, 0) && r.EndTime == new TimeOnly(21, 0)
                && r.ResourceTemplates.Single().StartTime == new TimeOnly(18, 30)
                && r.ResourceTemplates.Single().EndTime == new TimeOnly(20, 30)));
        }

        #endregion

        #region Ugyldige verdier

        public static TheoryData<string, string, string, string, string> InvalidValues() => new()
        {
            // metode, url, gyldig JSON, sti, ugyldig JSON-verdi
            { "POST", "/api/events", ValidEvent, "startTime", "\"ikke-en-tid\"" },
            { "POST", "/api/events", ValidEvent, "endTime", "\"\"" },
            { "POST", "/api/events", ValidEvent, "startTime", "null" },
            { "POST", "/api/events", ValidEvent, "startTime", "\"2026-13-01T08:00\"" },
            { "POST", "/api/events", ValidEvent, "startTime", "\"15.01.2026 08:00\"" },
            // Dagens frontendformat for vakter (dato og tid) er ikke lenger gyldig: bare klokkeslett.
            { "POST", "/api/events", ValidEvent, "resources[0].startTime", "\"2026-01-15T23:00\"" },
            { "POST", "/api/events", ValidEvent, "resources[0].endTime", "\"25:00\"" },
            { "PUT", "/api/events/5", ValidEvent, "startTime", "\"ikke-en-tid\"" },
            { "PUT", "/api/events/5", ValidEvent, "resources[0].endTime", "\"\"" },
            { "POST", "/api/events/template/3", """{ "startDate": "2026-10-07" }""", "startDate", "\"15.01.2026\"" },
            { "POST", "/api/events/template/3", """{ "startDate": "2026-10-07" }""", "startDate", "\"2026-10-07T00:00\"" },
            { "POST", "/api/events/template/3", """{ "startDate": "2026-10-07" }""", "startDate", "\"\"" },
            { "POST", "/api/templates", ValidTemplate, "startTime", "\"2000-01-01T18:00\"" },
            { "PUT", "/api/templates/2", ValidTemplate, "endTime", "\"ikke-en-tid\"" },
            { "PUT", "/api/templates/2", ValidTemplate, "resourceTemplates[0].startTime", "\"24:00\"" },
        };

        [Theory]
        [MemberData(nameof(InvalidValues))]
        public async Task InvalidTimeValue_Returns400WithNorwegianFieldError_AndDoesNotCallService(
            string method, string url, string validJson, string path, string invalidValue)
        {
            var response = await Send(method, url, With(validJson, path, invalidValue));

            var errors = Errors(await AssertValidationProblem(response));
            Assert.Equal([ModelValidation.InvalidValueMessage], Assert.Single(errors).Value);
            Assert.Equal(path, errors.Keys.Single());
            Assert.Empty(_events.ReceivedCalls());
            Assert.Empty(_templates.ReceivedCalls());
        }

        public static TheoryData<string, string, string, string> MissingValues() => new()
        {
            // metode, url, gyldig JSON, sti som fjernes
            { "POST", "/api/events", ValidEvent, "startTime" },
            { "PUT", "/api/events/5", ValidEvent, "endTime" },
            { "PUT", "/api/events/5", ValidEvent, "resources[0].startTime" },
            { "POST", "/api/events/template/3", """{ "startDate": "2026-10-07" }""", "startDate" },
            { "POST", "/api/templates", ValidTemplate, "startTime" },
            { "PUT", "/api/templates/2", ValidTemplate, "resourceTemplates[0].endTime" },
        };

        [Theory]
        [MemberData(nameof(MissingValues))]
        public async Task MissingTimeValue_Returns400WithNorwegianFieldError_AndDoesNotCallService(
            string method, string url, string validJson, string path)
        {
            var response = await Send(method, url, With(validJson, path, null));

            var errors = Errors(await AssertValidationProblem(response));
            Assert.Equal([ModelValidation.MissingValueMessage], Assert.Single(errors).Value);
            Assert.Equal(path, errors.Keys.Single());
            Assert.Empty(_events.ReceivedCalls());
            Assert.Empty(_templates.ReceivedCalls());
        }

        [Fact]
        public async Task SeveralMissingValues_GiveOneErrorPerField()
        {
            var json = With(With(ValidEvent, "startTime", null), "endTime", null);

            var response = await Send("POST", "/api/events", json);

            var errors = Errors(await AssertValidationProblem(response));
            Assert.Equal([ModelValidation.MissingValueMessage], errors["startTime"]);
            Assert.Equal([ModelValidation.MissingValueMessage], errors["endTime"]);
        }

        [Fact]
        public async Task UpdateEvent_WithInvalidResourceTime_NeverReachesService()
        {
            // Bindingen feiler før servicen kalles, så ingenting kan bli halvveis endret.
            var response = await Send("PUT", "/api/events/5", With(ValidEvent, "resources[0].startTime", "\"ikke-en-tid\""));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            await _events.DidNotReceiveWithAnyArgs().UpdateEvent(default, default!);
        }

        #endregion

        #region Øvrige valideringsfeil

        [Fact]
        public async Task MalformedJson_Returns400WithRootError()
        {
            var response = await Send("POST", "/api/events", "{ \"name\" }");

            var errors = Errors(await AssertValidationProblem(response));
            Assert.Equal([ModelValidation.InvalidBodyMessage], Assert.Single(errors, e => e.Key == ModelValidation.RootKey).Value);
        }

        [Fact]
        public async Task InvalidJsonRoot_Returns400WithRootError()
        {
            var response = await Send("POST", "/api/events", "[]");

            var errors = Errors(await AssertValidationProblem(response));
            Assert.Equal([ModelValidation.InvalidBodyMessage], Assert.Single(errors, e => e.Key == ModelValidation.RootKey).Value);
            Assert.DoesNotContain("request", errors.Keys);
        }

        [Fact]
        public async Task NullBody_Returns400WithMissingBodyMessage()
        {
            var response = await Send("POST", "/api/events", "null");

            var errors = Errors(await AssertValidationProblem(response));
            Assert.Equal([ModelValidation.MissingBodyMessage], Assert.Single(errors).Value);
        }

        [Fact]
        public async Task EmptyBody_Returns400WithNorwegianMessage()
        {
            var response = await Send("POST", "/api/events", "");

            var errors = Errors(await AssertValidationProblem(response));
            Assert.Equal([ModelValidation.MissingBodyMessage], Assert.Single(errors, e => e.Key == ModelValidation.RootKey).Value);
        }

        [Fact]
        public async Task InvalidQueryValue_Returns400WithNorwegianFieldError()
        {
            var response = await _client.GetAsync("/api/eventstatus?month=abc&year=2026");

            var errors = Errors(await AssertValidationProblem(response));
            Assert.Equal([ModelValidation.InvalidValueMessage], errors["month"]);
        }

        [Fact]
        public async Task DataAnnotationsError_KeepsMessage_WithJsonFieldName()
        {
            var response = await Send("POST", "/api/resources/1/messages", """{ "message": "" }""");

            var errors = Errors(await AssertValidationProblem(response));
            Assert.Single(Assert.Single(errors, e => e.Key == "message").Value);
        }

        #endregion
    }
}
