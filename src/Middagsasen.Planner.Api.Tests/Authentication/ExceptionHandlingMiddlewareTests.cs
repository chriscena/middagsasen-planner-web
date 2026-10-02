using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Services;
using NSubstitute;

namespace Middagsasen.Planner.Api.Tests.Authentication
{
    public class ExceptionHandlingMiddlewareTests
    {
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;
        private readonly IServiceProvider _services;

        public ExceptionHandlingMiddlewareTests()
        {
            _logger = Substitute.For<ILogger<ExceptionHandlingMiddleware>>();
            _services = new ServiceCollection()
                .AddLogging()
                .AddProblemDetails()
                .BuildServiceProvider();
        }

        private ExceptionHandlingMiddleware CreateMiddleware(RequestDelegate next)
        {
            return new ExceptionHandlingMiddleware(next, _logger, _services.GetRequiredService<IProblemDetailsService>());
        }

        private DefaultHttpContext CreateHttpContext()
        {
            var context = new DefaultHttpContext { RequestServices = _services };
            context.Response.Body = new MemoryStream();
            return context;
        }

        private static async Task<(int statusCode, string body)> GetResponse(DefaultHttpContext context)
        {
            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
            return (context.Response.StatusCode, body);
        }

        [Fact]
        public async Task Returns404_WhenEntityNotFoundExceptionThrown()
        {
            var middleware = CreateMiddleware(_ => throw new EntityNotFoundException("Not found"));
            var context = CreateHttpContext();

            await middleware.Invoke(context);

            var (statusCode, body) = await GetResponse(context);
            Assert.Equal(StatusCodes.Status404NotFound, statusCode);
            Assert.Contains("Not found", body);
        }

        [Fact]
        public async Task Returns403_WhenForbiddenAccessExceptionThrown()
        {
            var middleware = CreateMiddleware(_ => throw new ForbiddenAccessException("Forbidden"));
            var context = CreateHttpContext();

            await middleware.Invoke(context);

            var (statusCode, body) = await GetResponse(context);
            Assert.Equal(StatusCodes.Status403Forbidden, statusCode);
            Assert.Contains("Forbidden", body);
        }

        [Fact]
        public async Task Returns409_WhenEntityLockedExceptionThrown()
        {
            var middleware = CreateMiddleware(_ => throw new EntityLockedException("Locked"));
            var context = CreateHttpContext();

            await middleware.Invoke(context);

            var (statusCode, body) = await GetResponse(context);
            Assert.Equal(StatusCodes.Status409Conflict, statusCode);
            Assert.Contains("Locked", body);
        }

        [Fact]
        public async Task Returns401_WhenUnauthorizedAccessExceptionThrown()
        {
            var middleware = CreateMiddleware(_ => throw new UnauthorizedAccessException("Unauthorized"));
            var context = CreateHttpContext();

            await middleware.Invoke(context);

            var (statusCode, body) = await GetResponse(context);
            Assert.Equal(StatusCodes.Status401Unauthorized, statusCode);
            Assert.Contains("Unauthorized", body);
        }

        [Fact]
        public async Task Returns400_WhenInvalidOperationExceptionThrown()
        {
            var middleware = CreateMiddleware(_ => throw new InvalidOperationException("Bad request"));
            var context = CreateHttpContext();

            await middleware.Invoke(context);

            var (statusCode, body) = await GetResponse(context);
            Assert.Equal(StatusCodes.Status400BadRequest, statusCode);
            Assert.Contains("Bad request", body);
        }

        [Fact]
        public async Task Returns500_WhenUnexpectedExceptionThrown()
        {
            var middleware = CreateMiddleware(_ => throw new Exception("something secret"));
            var context = CreateHttpContext();

            await middleware.Invoke(context);

            var (statusCode, body) = await GetResponse(context);
            Assert.Equal(StatusCodes.Status500InternalServerError, statusCode);
            Assert.Contains("An unexpected error occurred.", body);
            Assert.DoesNotContain("something secret", body);
        }

        [Fact]
        public async Task Returns500_AndLogsError_WhenUnexpectedExceptionThrown()
        {
            var middleware = CreateMiddleware(_ => throw new Exception("something"));
            var context = CreateHttpContext();

            await middleware.Invoke(context);

            _logger.Received(1).Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception?, string>>());
        }

        [Fact]
        public async Task DoesNotInterfere_WhenNoExceptionThrown()
        {
            var middleware = CreateMiddleware(_ => Task.CompletedTask);
            var context = CreateHttpContext();

            await middleware.Invoke(context);

            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        }

        [Fact]
        public async Task ResponseContentTypeIsProblemJson()
        {
            var middleware = CreateMiddleware(_ => throw new InvalidOperationException("test"));
            var context = CreateHttpContext();

            await middleware.Invoke(context);

            Assert.Equal("application/problem+json", context.Response.ContentType);
        }

        [Fact]
        public async Task ResponseBodyIsProblemDetails()
        {
            var middleware = CreateMiddleware(_ => throw new EntityNotFoundException("Fant ikke vakt."));
            var context = CreateHttpContext();

            await middleware.Invoke(context);

            var (_, body) = await GetResponse(context);
            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;
            Assert.Equal(404, root.GetProperty("status").GetInt32());
            Assert.Equal("Not Found", root.GetProperty("title").GetString());
            Assert.Equal("Fant ikke vakt.", root.GetProperty("detail").GetString());
        }

        [Fact]
        public async Task ResponseBodyHasGenericDetail_WhenUnexpectedExceptionThrown()
        {
            var middleware = CreateMiddleware(_ => throw new Exception("something secret"));
            var context = CreateHttpContext();

            await middleware.Invoke(context);

            var (_, body) = await GetResponse(context);
            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;
            Assert.Equal(500, root.GetProperty("status").GetInt32());
            Assert.Equal("Internal Server Error", root.GetProperty("title").GetString());
            Assert.Equal("An unexpected error occurred.", root.GetProperty("detail").GetString());
        }

        [Fact]
        public async Task WritesProblemDetails_EvenWhenAcceptHeaderDoesNotAllowJson()
        {
            var middleware = CreateMiddleware(_ => throw new EntityNotFoundException("Fant ikke vakt."));
            var context = CreateHttpContext();
            context.Request.Headers.Accept = "text/html";

            await middleware.Invoke(context);

            var (statusCode, body) = await GetResponse(context);
            Assert.Equal(StatusCodes.Status404NotFound, statusCode);
            Assert.Equal("application/problem+json", context.Response.ContentType);
            using var json = JsonDocument.Parse(body);
            Assert.Equal("Fant ikke vakt.", json.RootElement.GetProperty("detail").GetString());
        }
    }
}
