using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Services;
using NSubstitute;

namespace Middagsasen.Planner.Api.Tests.Authentication
{
    public class ExceptionHandlingMiddlewareTests
    {
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddlewareTests()
        {
            _logger = Substitute.For<ILogger<ExceptionHandlingMiddleware>>();
        }

        private ExceptionHandlingMiddleware CreateMiddleware(RequestDelegate next)
        {
            return new ExceptionHandlingMiddleware(next, _logger);
        }

        private static DefaultHttpContext CreateHttpContext()
        {
            var context = new DefaultHttpContext();
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
        public async Task ResponseContentTypeIsJson()
        {
            var middleware = CreateMiddleware(_ => throw new InvalidOperationException("test"));
            var context = CreateHttpContext();

            await middleware.Invoke(context);

            Assert.Equal("application/json", context.Response.ContentType);
        }
    }
}
