using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Middagsasen.Planner.Api.Services.Reminders;

namespace Middagsasen.Planner.Api.Tests.Services.Reminders
{
    public class ReminderOptionsValidatorTests
    {
        private static ValidateOptionsResult Validate(ReminderOptions options) => new ReminderOptionsValidator().Validate(null, options);

        [Fact]
        public void DefaultOptions_AreValid()
        {
            var options = new ReminderOptions();

            Assert.True(Validate(options).Succeeded);
            Assert.True(options.Enabled);
            Assert.Equal(new TimeSpan(17, 0, 0), options.SendTime);
            Assert.Equal(new TimeSpan(22, 0, 0), options.RetryUntil);
            Assert.Equal(TimeSpan.FromMinutes(5), options.PollInterval);
        }

        public static TheoryData<string, ReminderOptions> InvalidOptions => new()
        {
            { nameof(ReminderOptions.SendTime), new ReminderOptions { SendTime = TimeSpan.FromMinutes(-1) } },
            { nameof(ReminderOptions.SendTime), new ReminderOptions { SendTime = TimeSpan.FromDays(1), RetryUntil = TimeSpan.FromDays(1) + TimeSpan.FromHours(1) } },
            { nameof(ReminderOptions.RetryUntil), new ReminderOptions { RetryUntil = TimeSpan.FromDays(1) } },
            { nameof(ReminderOptions.SendTime), new ReminderOptions { SendTime = new TimeSpan(22, 0, 0), RetryUntil = new TimeSpan(17, 0, 0) } },
            { nameof(ReminderOptions.SendTime), new ReminderOptions { SendTime = new TimeSpan(17, 0, 0), RetryUntil = new TimeSpan(17, 0, 0) } },
            { nameof(ReminderOptions.PollInterval), new ReminderOptions { PollInterval = TimeSpan.Zero } },
            { nameof(ReminderOptions.PollInterval), new ReminderOptions { PollInterval = TimeSpan.FromSeconds(-5) } },
            { nameof(ReminderOptions.PollInterval), new ReminderOptions { PollInterval = TimeSpan.FromDays(1) + TimeSpan.FromSeconds(1) } },
        };

        [Theory]
        [MemberData(nameof(InvalidOptions))]
        public void InvalidValue_FailsWithMessageNamingTheSetting(string property, ReminderOptions options)
        {
            var result = Validate(options);

            Assert.True(result.Failed);
            Assert.Contains(result.Failures!, f => f.Contains($"Reminders:{property}"));
        }

        [Fact]
        public void SendTimeAtMidnight_AndRetryUntilLateEvening_AreValid()
        {
            Assert.True(Validate(new ReminderOptions { SendTime = TimeSpan.Zero, RetryUntil = new TimeSpan(23, 59, 59) }).Succeeded);
        }

        [Fact]
        public void PollIntervalOfOneDay_IsValid()
        {
            Assert.True(Validate(new ReminderOptions { PollInterval = TimeSpan.FromDays(1) }).Succeeded);
        }

        [Fact]
        public void BoundFromConfiguration_PlainNumberPollIntervalIsDays_AndRejected()
        {
            // "60" tolkes som 60 dager. PeriodicTimer kaster for så lange perioder, og det ville stoppet appen.
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Reminders:PollInterval"] = "60",
            }).Build();

            var ex = Assert.Throws<OptionsValidationException>(() => BuildOptions(configuration).Value);
            Assert.Contains("Reminders:PollInterval", ex.Message);
            Assert.Contains("dager", ex.Message);
        }

        [Fact]
        public void ReportsAllErrors()
        {
            var result = Validate(new ReminderOptions { SendTime = TimeSpan.FromDays(2), RetryUntil = TimeSpan.FromDays(1), PollInterval = TimeSpan.Zero });

            // SendTime utenfor døgnet, RetryUntil utenfor døgnet, SendTime ikke før RetryUntil, PollInterval ikke positiv.
            Assert.Equal(4, result.Failures!.Count());
        }

        [Fact]
        public void BoundFromConfiguration_ClockTimesAreParsed()
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Reminders:Enabled"] = "false",
                ["Reminders:SendTime"] = "18:30",
                ["Reminders:RetryUntil"] = "21:00",
                ["Reminders:PollInterval"] = "00:01:00",
            }).Build();
            var options = BuildOptions(configuration).Value;

            Assert.False(options.Enabled);
            Assert.Equal(new TimeSpan(18, 30, 0), options.SendTime);
            Assert.Equal(new TimeSpan(21, 0, 0), options.RetryUntil);
            Assert.Equal(TimeSpan.FromMinutes(1), options.PollInterval);
        }

        [Fact]
        public void BoundFromConfiguration_PlainNumberIsDays_AndRejectedAsClockTime()
        {
            // Dokumentert fallgruve: "17" tolkes som 17 dager, ikke kl. 17, og avvises som klokkeslett.
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Reminders:SendTime"] = "17",
            }).Build();

            var ex = Assert.Throws<OptionsValidationException>(() => BuildOptions(configuration).Value);
            Assert.Contains("Reminders:SendTime", ex.Message);
        }

        private static IOptions<ReminderOptions> BuildOptions(IConfiguration configuration)
        {
            var services = new ServiceCollection();
            services.AddOptions<ReminderOptions>().Bind(configuration.GetSection(ReminderOptions.SectionName));
            services.AddSingleton<IValidateOptions<ReminderOptions>, ReminderOptionsValidator>();
            return services.BuildServiceProvider().GetRequiredService<IOptions<ReminderOptions>>();
        }
    }
}
