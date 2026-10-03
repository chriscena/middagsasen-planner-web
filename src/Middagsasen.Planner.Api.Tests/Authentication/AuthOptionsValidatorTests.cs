using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Middagsasen.Planner.Api.Authentication;

namespace Middagsasen.Planner.Api.Tests.Authentication
{
    public class AuthOptionsValidatorTests
    {
        private static ValidateOptionsResult Validate(AuthOptions options) => new AuthOptionsValidator().Validate(null, options);

        [Fact]
        public void DefaultOptions_AreValid()
        {
            Assert.True(Validate(new AuthOptions()).Succeeded);
        }

        public static TheoryData<string, AuthOptions> InvalidOptions => new()
        {
            { nameof(AuthOptions.Issuer), new AuthOptions { Issuer = " " } },
            { nameof(AuthOptions.Audience), new AuthOptions { Audience = "" } },
            { nameof(AuthOptions.TokenLifetime), new AuthOptions { TokenLifetime = TimeSpan.Zero } },
            { nameof(AuthOptions.OtpThrottle), new AuthOptions { OtpThrottle = TimeSpan.FromMinutes(-1) } },
            { nameof(AuthOptions.OtpLifetime), new AuthOptions { OtpLifetime = TimeSpan.Zero } },
            { nameof(AuthOptions.MaxOtpAttempts), new AuthOptions { MaxOtpAttempts = 0 } },
        };

        [Theory]
        [MemberData(nameof(InvalidOptions))]
        public void InvalidValue_FailsWithMessageNamingTheSetting(string property, AuthOptions options)
        {
            var result = Validate(options);

            Assert.True(result.Failed);
            var failure = Assert.Single(result.Failures!);
            Assert.Contains($"Auth:{property}", failure);
        }

        [Fact]
        public void ReportsAllErrors()
        {
            var result = Validate(new AuthOptions { Issuer = "", TokenLifetime = TimeSpan.Zero, MaxOtpAttempts = 0 });

            Assert.Equal(3, result.Failures!.Count());
        }

        [Fact]
        public void BoundFromConfiguration_TimeSpanStringIsParsedAsTimeOfDay_AndPlainNumberAsDays()
        {
            // Dokumentert fallgruve: "30" tolkes som 30 dager, ikke 30 minutter.
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:OtpLifetime"] = "00:30:00",
                ["Auth:OtpThrottle"] = "30",
            }).Build();
            var options = BuildOptions(configuration).Value;

            Assert.Equal(TimeSpan.FromMinutes(30), options.OtpLifetime);
            Assert.Equal(TimeSpan.FromDays(30), options.OtpThrottle);
        }

        [Fact]
        public void BoundFromConfiguration_InvalidValueThrowsOptionsValidationException()
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:MaxOtpAttempts"] = "0",
            }).Build();

            var ex = Assert.Throws<OptionsValidationException>(() => BuildOptions(configuration).Value);
            Assert.Contains("Auth:MaxOtpAttempts", ex.Message);
        }

        private static IOptions<AuthOptions> BuildOptions(IConfiguration configuration)
        {
            var services = new ServiceCollection();
            services.AddOptions<AuthOptions>().Bind(configuration.GetSection(AuthOptions.SectionName));
            services.AddSingleton<IValidateOptions<AuthOptions>, AuthOptionsValidator>();
            return services.BuildServiceProvider().GetRequiredService<IOptions<AuthOptions>>();
        }
    }
}
