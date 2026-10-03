using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Tests.Infrastructure;
using NSubstitute;

namespace Middagsasen.Planner.Api.Tests.Authentication
{
    public class SessionTokensTests
    {
        private static readonly string Secret = new('x', 64);
        private static readonly DateTimeOffset Now = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

        private static SessionTokens CreateTokens(
            string? secret = null,
            string issuer = "middagsasen-planner",
            string audience = "middagsasen-planner-web",
            TimeProvider? timeProvider = null)
        {
            var authSettings = Substitute.For<IAuthSettings>();
            authSettings.Secret.Returns(secret ?? Secret);
            var options = Options.Create(new AuthOptions { Issuer = issuer, Audience = audience });
            return new SessionTokens(authSettings, options, timeProvider ?? new FakeTimeProvider(Now));
        }

        [Fact]
        public void ReadSessionId_ReturnsSameSessionId_AsCreated()
        {
            var tokens = CreateTokens();
            var sessionId = Guid.NewGuid();

            var token = tokens.Create(sessionId);

            Assert.Equal(sessionId, tokens.ReadSessionId(token));
        }

        [Fact]
        public void Create_SetsIssuerAudienceAndLifetime()
        {
            var token = new JwtSecurityTokenHandler().ReadJwtToken(CreateTokens().Create(Guid.NewGuid()));

            Assert.Equal("middagsasen-planner", token.Issuer);
            Assert.Equal(new[] { "middagsasen-planner-web" }, token.Audiences);
            Assert.Equal(Now.UtcDateTime, token.IssuedAt);
            Assert.Equal(Now.UtcDateTime, token.ValidFrom);
            Assert.Equal(Now.UtcDateTime + new AuthOptions().TokenLifetime, token.ValidTo);
            Assert.Equal(SecurityAlgorithms.HmacSha256, token.Header.Alg);
        }

        [Fact]
        public void ReadSessionId_ReturnsNull_ForTokenSignedWithOtherKey()
        {
            var token = CreateTokens(secret: new string('y', 64)).Create(Guid.NewGuid());

            Assert.Null(CreateTokens().ReadSessionId(token));
        }

        [Fact]
        public void ReadSessionId_ReturnsNull_ForOtherIssuer()
        {
            var token = CreateTokens(issuer: "noen-andre").Create(Guid.NewGuid());

            Assert.Null(CreateTokens().ReadSessionId(token));
        }

        [Fact]
        public void ReadSessionId_ReturnsNull_ForOtherAudience()
        {
            var token = CreateTokens(audience: "noen-andre").Create(Guid.NewGuid());

            Assert.Null(CreateTokens().ReadSessionId(token));
        }

        [Fact]
        public void ReadSessionId_ReturnsSessionId_JustBeforeExpiry()
        {
            var token = CreateTokens().Create(Guid.NewGuid());
            var justBefore = new FakeTimeProvider(Now + new AuthOptions().TokenLifetime - TimeSpan.FromSeconds(1));

            Assert.NotNull(CreateTokens(timeProvider: justBefore).ReadSessionId(token));
        }

        [Fact]
        public void ReadSessionId_ReturnsNull_WhenExpired()
        {
            var token = CreateTokens().Create(Guid.NewGuid());
            var expired = new FakeTimeProvider(Now + new AuthOptions().TokenLifetime);

            Assert.Null(CreateTokens(timeProvider: expired).ReadSessionId(token));
        }

        [Fact]
        public void ReadSessionId_ReturnsNull_BeforeNotBefore()
        {
            var token = CreateTokens().Create(Guid.NewGuid());
            var before = new FakeTimeProvider(Now - TimeSpan.FromSeconds(1));

            Assert.Null(CreateTokens(timeProvider: before).ReadSessionId(token));
        }

        [Fact]
        public void ReadSessionId_ReturnsNull_WhenExpiredBySystemClock()
        {
            // Utstedt med en klokke langt tilbake i tid, validert med systemklokka.
            var token = CreateTokens(timeProvider: new FakeTimeProvider(Now.AddYears(-1))).Create(Guid.NewGuid());

            Assert.Null(CreateTokens(timeProvider: TimeProvider.System).ReadSessionId(token));
        }

        [Theory]
        [InlineData("")]
        [InlineData("ikke-et-token")]
        [InlineData("a.b.c")]
        [InlineData("eyJhbGciOiJIUzI1NiJ9.e30.")]
        public void ReadSessionId_ReturnsNull_ForMalformedToken(string token)
        {
            Assert.Null(CreateTokens().ReadSessionId(token));
        }

        [Fact]
        public void ReadSessionId_ReturnsNull_WhenIdClaimMissing()
        {
            Assert.Null(CreateTokens().ReadSessionId(CreateSignedToken(new Claim("sub", "42"))));
        }

        [Fact]
        public void ReadSessionId_ReturnsNull_WhenIdClaimIsNotGuid()
        {
            Assert.Null(CreateTokens().ReadSessionId(CreateSignedToken(new Claim("id", "42"))));
        }

        [Fact]
        public void ReadSessionId_ReturnsNull_ForUnsignedToken()
        {
            var handler = new JwtSecurityTokenHandler();
            var token = handler.WriteToken(handler.CreateJwtSecurityToken(new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[] { new Claim("id", Guid.NewGuid().ToString()) }),
                Issuer = "middagsasen-planner",
                Audience = "middagsasen-planner-web",
                IssuedAt = Now.UtcDateTime,
                NotBefore = Now.UtcDateTime,
                Expires = Now.UtcDateTime.AddHours(1),
            }));

            Assert.Null(CreateTokens().ReadSessionId(token));
        }

        [Fact]
        public void Create_Throws_WhenSecretIsMissing()
        {
            Assert.Throws<InvalidOperationException>(() => CreateTokens(secret: string.Empty).Create(Guid.NewGuid()));
        }

        /// <summary>Gyldig signert token med riktig utsteder og mottaker, men med valgfrie claims.</summary>
        private static string CreateSignedToken(params Claim[] claims)
        {
            var handler = new JwtSecurityTokenHandler();
            return handler.WriteToken(handler.CreateToken(new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Issuer = "middagsasen-planner",
                Audience = "middagsasen-planner-web",
                IssuedAt = Now.UtcDateTime,
                NotBefore = Now.UtcDateTime,
                Expires = Now.UtcDateTime.AddHours(1),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.ASCII.GetBytes(Secret)), SecurityAlgorithms.HmacSha256),
            }));
        }
    }
}
