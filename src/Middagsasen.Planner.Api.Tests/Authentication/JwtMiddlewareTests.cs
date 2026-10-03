using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Services;
using Middagsasen.Planner.Api.Services.Authentication;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Middagsasen.Planner.Api.Tests.Authentication
{
    public class JwtMiddlewareTests
    {
        private const string ValidToken = "gyldig-token";
        private static readonly Guid SessionId = Guid.NewGuid();

        private readonly ISessionTokens _sessionTokens = Substitute.For<ISessionTokens>();
        private readonly IAuthenticationService _authService = Substitute.For<IAuthenticationService>();
        private readonly ILogger<JwtMiddleware> _logger = Substitute.For<ILogger<JwtMiddleware>>();
        private bool _nextCalled;

        public JwtMiddlewareTests()
        {
            _sessionTokens.ReadSessionId(Arg.Any<string>()).Returns((Guid?)null);
            _sessionTokens.ReadSessionId(ValidToken).Returns(SessionId);
            _authService.GetUserBySessionId(SessionId).Returns(new Actor(42, IsAdmin: true));
        }

        private JwtMiddleware CreateMiddleware() => new(_ => { _nextCalled = true; return Task.CompletedTask; }, _logger);

        private static DefaultHttpContext CreateHttpContext(string? authorization)
        {
            var context = new DefaultHttpContext();
            if (authorization != null) context.Request.Headers.Authorization = authorization;
            return context;
        }

        [Theory]
        [InlineData("Bearer " + ValidToken)]
        [InlineData("bearer " + ValidToken)]
        public async Task SetsUser_ForValidBearerToken(string authorization)
        {
            var context = CreateHttpContext(authorization);

            await CreateMiddleware().Invoke(context, _sessionTokens, _authService);

            var actor = Assert.IsType<Actor>(context.Items["User"]);
            Assert.Equal(42, actor.UserId);
            Assert.True(actor.IsAdmin);
            Assert.True(context.User.IsInRole(Roles.Administrator));
            Assert.True(_nextCalled);
        }

        [Fact]
        public async Task ContinuesAnonymously_AndLogs_ForInvalidToken()
        {
            var context = CreateHttpContext("Bearer ugyldig-token");

            await CreateMiddleware().Invoke(context, _sessionTokens, _authService);

            Assert.False(context.Items.ContainsKey("User"));
            Assert.True(_nextCalled);
            await _authService.DidNotReceive().GetUserBySessionId(Arg.Any<Guid>());
            _logger.Received(1).Log(
                LogLevel.Information,
                Arg.Any<EventId>(),
                Arg.Is<object>(state => !state.ToString()!.Contains("ugyldig-token")),
                null,
                Arg.Any<Func<object, Exception?, string>>());
        }

        [Fact]
        public async Task ContinuesAnonymously_WhenSessionNoLongerExists()
        {
            _authService.GetUserBySessionId(SessionId).Returns((Actor?)null);
            var context = CreateHttpContext("Bearer " + ValidToken);

            await CreateMiddleware().Invoke(context, _sessionTokens, _authService);

            Assert.False(context.Items.ContainsKey("User"));
            Assert.True(_nextCalled);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(ValidToken)]
        [InlineData("Basic " + ValidToken)]
        [InlineData("Token " + ValidToken)]
        [InlineData("Bearer")]
        [InlineData("Bearer ")]
        public async Task IgnoresHeader_WhenNotBearerToken(string? authorization)
        {
            var context = CreateHttpContext(authorization);

            await CreateMiddleware().Invoke(context, _sessionTokens, _authService);

            Assert.False(context.Items.ContainsKey("User"));
            Assert.True(_nextCalled);
            _sessionTokens.DidNotReceive().ReadSessionId(Arg.Any<string>());
        }

        [Fact]
        public async Task DoesNotSwallow_ErrorsWhenLookingUpSession()
        {
            // Databasefeil skal boble videre til ExceptionHandlingMiddleware, ikke gi en anonym forespørsel.
            _authService.GetUserBySessionId(SessionId).ThrowsAsync(new InvalidOperationException("Databasen er nede"));
            var context = CreateHttpContext("Bearer " + ValidToken);

            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateMiddleware().Invoke(context, _sessionTokens, _authService));

            Assert.False(_nextCalled);
        }
    }
}
