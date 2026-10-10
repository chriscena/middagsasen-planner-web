using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Middagsasen.Planner.Api.Services.StaffingAlerts;

namespace Middagsasen.Planner.Api.Tests.Services.StaffingAlerts
{
    public class StaffingAlertOptionsValidatorTests
    {
        private static ValidateOptionsResult Validate(StaffingAlertOptions options) => new StaffingAlertOptionsValidator().Validate(null, options);

        [Fact]
        public void DefaultOptions_AreValid()
        {
            var options = new StaffingAlertOptions();

            Assert.True(Validate(options).Succeeded);
            Assert.Equal(2, options.NoticeDays);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(-10)]
        public void NegativeNoticeDays_FailsWithMessageNamingTheSetting(int noticeDays)
        {
            var result = Validate(new StaffingAlertOptions { NoticeDays = noticeDays });

            Assert.True(result.Failed);
            var failure = Assert.Single(result.Failures!);
            Assert.Contains("StaffingAlerts:NoticeDays", failure);
            Assert.Contains(noticeDays.ToString(), failure);
        }

        [Fact]
        public void ZeroNoticeDays_IsValid()
        {
            Assert.True(Validate(new StaffingAlertOptions { NoticeDays = 0 }).Succeeded);
        }

        [Fact]
        public void BoundFromConfiguration_NoticeDaysIsParsed()
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["StaffingAlerts:NoticeDays"] = "3",
            }).Build();

            Assert.Equal(3, BuildOptions(configuration).Value.NoticeDays);
        }

        [Fact]
        public void BoundFromEmptyConfiguration_UsesDefault()
        {
            var configuration = new ConfigurationBuilder().Build();

            Assert.Equal(2, BuildOptions(configuration).Value.NoticeDays);
        }

        [Fact]
        public void BoundFromConfiguration_NegativeNoticeDays_IsRejected()
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["StaffingAlerts:NoticeDays"] = "-1",
            }).Build();

            var ex = Assert.Throws<OptionsValidationException>(() => BuildOptions(configuration).Value);
            Assert.Contains("StaffingAlerts:NoticeDays", ex.Message);
        }

        private static IOptions<StaffingAlertOptions> BuildOptions(IConfiguration configuration)
        {
            var services = new ServiceCollection();
            services.AddOptions<StaffingAlertOptions>().Bind(configuration.GetSection(StaffingAlertOptions.SectionName));
            services.AddSingleton<IValidateOptions<StaffingAlertOptions>, StaffingAlertOptionsValidator>();
            return services.BuildServiceProvider().GetRequiredService<IOptions<StaffingAlertOptions>>();
        }
    }
}
