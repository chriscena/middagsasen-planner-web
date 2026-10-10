using Middagsasen.Planner.Api.Services.Reminders;

namespace Middagsasen.Planner.Api.Tests.Services.Reminders
{
    public class ShiftReminderRulesTests
    {
        private static readonly ReminderOptions Options = new() { SendTime = new TimeSpan(17, 0, 0), RetryUntil = new TimeSpan(22, 0, 0) };

        // 14.10.2025 er en tirsdag (eksempelet i issue #42).
        private static readonly DateOnly Tuesday = new(2025, 10, 14);

        private static ReminderShift Shift(int startHour, int endHour, string resourceType = "storheis", string eventName = "Åpningstid", int startMinute = 0, int endMinute = 0)
            => new(Tuesday.ToDateTime(new TimeOnly(startHour, startMinute)), Tuesday.ToDateTime(new TimeOnly(endHour, endMinute)), resourceType, eventName);

        // --- Sendevindu ---

        [Theory]
        [InlineData(16, 59, 59, false)]
        [InlineData(17, 0, 0, true)]
        [InlineData(19, 30, 0, true)]
        [InlineData(21, 59, 59, true)]
        [InlineData(22, 0, 0, false)]
        [InlineData(23, 0, 0, false)]
        [InlineData(0, 0, 0, false)]
        public void IsSendWindowOpen_FromSendTime_UntilRetryUntil(int hour, int minute, int second, bool expected)
        {
            var nowLocal = new DateTime(2026, 10, 13, hour, minute, second);

            Assert.Equal(expected, ShiftReminderRules.IsSendWindowOpen(Options, nowLocal));
        }

        // --- Vaktdag ---

        [Theory]
        [InlineData(2026, 10, 13, 17, 5, 2026, 10, 14)]
        [InlineData(2026, 10, 13, 0, 0, 2026, 10, 14)]
        [InlineData(2026, 10, 13, 23, 59, 2026, 10, 14)]
        [InlineData(2026, 12, 31, 18, 0, 2027, 1, 1)]
        public void ShiftDateFor_IsTomorrow(int year, int month, int day, int hour, int minute, int expectedYear, int expectedMonth, int expectedDay)
        {
            var shiftDate = ShiftReminderRules.ShiftDateFor(new DateTime(year, month, day, hour, minute, 0));

            Assert.Equal(new DateOnly(expectedYear, expectedMonth, expectedDay), shiftDate);
        }

        // --- Effektive tider ---

        [Fact]
        public void EffectivePeriod_UsesShiftTimes_WhenSet()
        {
            var resourceStart = new DateTime(2026, 10, 14, 10, 0, 0);
            var resourceEnd = new DateTime(2026, 10, 14, 22, 0, 0);
            var shiftStart = new DateTime(2026, 10, 14, 18, 0, 0);
            var shiftEnd = new DateTime(2026, 10, 14, 20, 0, 0);

            var period = ShiftReminderRules.EffectivePeriod(shiftStart, shiftEnd, resourceStart, resourceEnd);

            Assert.Equal((shiftStart, shiftEnd), period);
        }

        [Fact]
        public void EffectivePeriod_UsesResourceTimes_WhenShiftTimesAreNull()
        {
            var resourceStart = new DateTime(2026, 10, 14, 10, 0, 0);
            var resourceEnd = new DateTime(2026, 10, 14, 22, 0, 0);

            var period = ShiftReminderRules.EffectivePeriod(null, null, resourceStart, resourceEnd);

            Assert.Equal((resourceStart, resourceEnd), period);
        }

        [Fact]
        public void EffectivePeriod_MixesShiftAndResourceTimes_PerEndpoint()
        {
            var resourceStart = new DateTime(2026, 10, 14, 10, 0, 0);
            var resourceEnd = new DateTime(2026, 10, 14, 22, 0, 0);
            var shiftStart = new DateTime(2026, 10, 14, 18, 0, 0);

            var period = ShiftReminderRules.EffectivePeriod(shiftStart, null, resourceStart, resourceEnd);

            Assert.Equal((shiftStart, resourceEnd), period);
        }

        [Fact]
        public void EffectivePeriod_ShiftStartingAfterMidnight_BelongsToNextDay()
        {
            // Oppgaven starter 14.10, men vakta starter etter midnatt: effektiv start er 15.10.
            var resourceStart = new DateTime(2026, 10, 14, 20, 0, 0);
            var resourceEnd = new DateTime(2026, 10, 15, 4, 0, 0);
            var shiftStart = new DateTime(2026, 10, 15, 0, 30, 0);

            var period = ShiftReminderRules.EffectivePeriod(shiftStart, null, resourceStart, resourceEnd);

            Assert.Equal(new DateOnly(2026, 10, 15), DateOnly.FromDateTime(period.Start));
        }

        // --- Meldingstekst ---

        [Fact]
        public void BuildMessage_SingleShift()
        {
            var message = ShiftReminderRules.BuildMessage("Kari", Tuesday, [Shift(18, 22)]);

            Assert.Equal("Hei Kari! Kjapp påminnelse om vakt i morgen, tirsdag 14.10: 18–22 storheis.", message);
        }

        [Fact]
        public void BuildMessage_TwoShifts_SortedByStart()
        {
            var message = ShiftReminderRules.BuildMessage("Kari", Tuesday, [Shift(18, 22), Shift(10, 14, "kiosk")]);

            Assert.Equal("Hei Kari! Kjapp påminnelse om vakt i morgen, tirsdag 14.10: 10–14 kiosk, 18–22 storheis.", message);
        }

        [Fact]
        public void BuildMessage_UsesMinutes_OnlyWhenNotWholeHour()
        {
            var message = ShiftReminderRules.BuildMessage("Kari", Tuesday, [Shift(18, 22, startMinute: 30)]);

            Assert.Equal("Hei Kari! Kjapp påminnelse om vakt i morgen, tirsdag 14.10: 18:30–22 storheis.", message);
        }

        [Fact]
        public void BuildMessage_IncludesEventName_WhenNotDefault()
        {
            var message = ShiftReminderRules.BuildMessage("Kari", Tuesday, [Shift(18, 22, eventName: "Diskokveld")]);

            Assert.Equal("Hei Kari! Kjapp påminnelse om vakt i morgen, tirsdag 14.10: 18–22 storheis (Diskokveld).", message);
        }

        [Theory]
        [InlineData("Åpningstid")]
        [InlineData("åpningstid")]
        [InlineData(" Åpningstid ")]
        [InlineData("")]
        [InlineData("   ")]
        public void BuildMessage_OmitsEventName_WhenDefaultOrEmpty(string eventName)
        {
            var message = ShiftReminderRules.BuildMessage("Kari", Tuesday, [Shift(18, 22, eventName: eventName)]);

            Assert.Equal("Hei Kari! Kjapp påminnelse om vakt i morgen, tirsdag 14.10: 18–22 storheis.", message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void BuildMessage_WithoutFirstName_GreetsWithoutName(string? firstName)
        {
            var message = ShiftReminderRules.BuildMessage(firstName, Tuesday, [Shift(18, 22)]);

            Assert.StartsWith("Hei! Kjapp påminnelse", message);
        }

        [Theory]
        [InlineData(2026, 10, 12, "mandag 12.10")]
        [InlineData(2025, 10, 14, "tirsdag 14.10")]
        [InlineData(2026, 10, 14, "onsdag 14.10")]
        [InlineData(2026, 10, 18, "søndag 18.10")]
        [InlineData(2027, 1, 1, "fredag 01.01")]
        public void BuildMessage_DayNameInNorwegianLowercase_AndDateAsDayDotMonth(int year, int month, int day, string expected)
        {
            var date = new DateOnly(year, month, day);

            var message = ShiftReminderRules.BuildMessage("Kari", date, [new ReminderShift(date.ToDateTime(new TimeOnly(18, 0)), date.ToDateTime(new TimeOnly(22, 0)), "storheis", "Åpningstid")]);

            Assert.Contains($"i morgen, {expected}:", message);
        }
    }
}
