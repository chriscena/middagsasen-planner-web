using Middagsasen.Planner.Api.Services.Reminders;
using Middagsasen.Planner.Api.Services.Shifts;
using Middagsasen.Planner.Api.Services.StaffingAlerts;

namespace Middagsasen.Planner.Api.Tests.Services.StaffingAlerts
{
    public class StaffingAlertRulesTests
    {
        // Nå er tirsdag 13.10.2026 kl. 12:00 norsk tid i de fleste testene.
        private static readonly DateTime Now = new(2026, 10, 13, 12, 0, 0);

        /// <summary>Oppgaven mangler bemanning: 1 av 2 vakter bemannet.</summary>
        private static readonly ResourceStaffing MissingStaff = new(ShiftCount: 2, StaffedCount: 1);

        /// <summary>En vakt 18–22 på dagen <paramref name="daysFromNow"/> dager fram, med oppgaven som slutter samtidig.</summary>
        private static (DateTime Start, DateTime End) ShiftOn(int daysFromNow)
        {
            var day = Now.Date.AddDays(daysFromNow);
            return (day.AddHours(18), day.AddHours(22));
        }

        /// <summary>Standardgrensen (<see cref="StaffingAlertOptions.NoticeDays"/>): til og med i overmorgen.</summary>
        private const int DefaultNoticeDays = 2;

        // --- IsDue: antall dager fram ---

        [Theory]
        [InlineData(0, true)]  // i dag
        [InlineData(1, true)]  // i morgen
        [InlineData(2, true)]  // om to dager (grensen)
        [InlineData(3, false)] // om tre dager
        [InlineData(7, false)]
        public void IsDue_WhenShiftStartsWithinNoticeDays(int daysFromNow, bool expected)
        {
            var (start, end) = ShiftOn(daysFromNow);

            Assert.Equal(expected, StaffingAlertRules.IsDue(Now, start, end, MissingStaff, noticeDays: DefaultNoticeDays));
        }

        [Fact]
        public void DefaultNoticeDays_IsTwo()
        {
            Assert.Equal(DefaultNoticeDays, new StaffingAlertOptions().NoticeDays);
        }

        [Theory]
        [InlineData(0, 0, true)]  // bare i dag
        [InlineData(0, 1, false)] // i morgen er utenfor
        [InlineData(5, 5, true)]  // om fem dager (grensen)
        [InlineData(5, 6, false)] // om seks dager
        public void IsDue_FollowsNoticeDays(int noticeDays, int daysFromNow, bool expected)
        {
            var (start, end) = ShiftOn(daysFromNow);

            Assert.Equal(expected, StaffingAlertRules.IsDue(Now, start, end, MissingStaff, noticeDays));
        }

        // --- IsDue: døgngrensen er kalenderdager i norsk tid, ikke 48 timer ---

        [Fact]
        public void IsDue_CountsCalendarDays_JustBeforeMidnight_TwoDaysAhead_IsDue()
        {
            var now = new DateTime(2026, 10, 13, 23, 59, 0);
            var start = new DateTime(2026, 10, 15, 0, 30, 0);
            var end = new DateTime(2026, 10, 15, 4, 0, 0);

            Assert.True(StaffingAlertRules.IsDue(now, start, end, MissingStaff, DefaultNoticeDays));
        }

        [Fact]
        public void IsDue_CountsCalendarDays_JustAfterMidnight_ThreeDaysAhead_IsNotDue()
        {
            var now = new DateTime(2026, 10, 13, 0, 1, 0);
            var start = new DateTime(2026, 10, 16, 0, 0, 0);
            var end = new DateTime(2026, 10, 16, 4, 0, 0);

            Assert.False(StaffingAlertRules.IsDue(now, start, end, MissingStaff, DefaultNoticeDays));
        }

        // --- IsDue: oppgavens status ---

        [Fact]
        public void IsDue_False_WhenTaskHasEnded()
        {
            var now = new DateTime(2026, 10, 13, 22, 0, 0);
            var start = new DateTime(2026, 10, 13, 18, 0, 0);
            var end = new DateTime(2026, 10, 13, 22, 0, 0);

            Assert.False(StaffingAlertRules.IsDue(now, start, end, MissingStaff, DefaultNoticeDays));
        }

        [Fact]
        public void IsDue_True_WhenShiftHasStarted_ButTaskHasNotEnded()
        {
            var now = new DateTime(2026, 10, 13, 19, 0, 0);
            var start = new DateTime(2026, 10, 13, 18, 0, 0);
            var end = new DateTime(2026, 10, 13, 22, 0, 0);

            Assert.True(StaffingAlertRules.IsDue(now, start, end, MissingStaff, DefaultNoticeDays));
        }

        [Theory]
        [InlineData(2, 2)] // full
        [InlineData(2, 3)] // overbooket: fortsatt full etter fjerningen
        [InlineData(0, 0)]
        public void IsDue_False_WhenTaskIsStillFull(int shiftCount, int staffedCount)
        {
            var (start, end) = ShiftOn(1);

            Assert.False(StaffingAlertRules.IsDue(Now, start, end, new ResourceStaffing(shiftCount, staffedCount), DefaultNoticeDays));
        }

        // --- BuildMessage ---

        // 14.10.2025 er en tirsdag (eksempelet i ordlista).
        private static readonly DateOnly Tuesday = new(2025, 10, 14);

        private static ReminderShift Shift(string eventName = "Diskokveld", int startMinute = 0)
            => new(Tuesday.ToDateTime(new TimeOnly(18, startMinute)), Tuesday.ToDateTime(new TimeOnly(22, 0)), "storheis", eventName);

        [Fact]
        public void BuildMessage_Singular_WithEventName()
        {
            var message = StaffingAlertRules.BuildMessage("Ola", "Kari Nordmann", Shift(), openShifts: 1);

            Assert.Equal("Hei Ola! Kari Nordmann har trukket seg fra vakt tirsdag 14.10: 18–22 storheis (Diskokveld). Oppgaven har nå 1 ledig vakt.", message);
        }

        [Fact]
        public void BuildMessage_Plural()
        {
            var message = StaffingAlertRules.BuildMessage("Ola", "Kari Nordmann", Shift(), openShifts: 2);

            Assert.EndsWith("Oppgaven har nå 2 ledige vakter.", message);
        }

        [Theory]
        [InlineData("Åpningstid")]
        [InlineData("åpningstid")]
        [InlineData(" Åpningstid ")]
        [InlineData("")]
        public void BuildMessage_OmitsEventName_WhenDefaultOrEmpty(string eventName)
        {
            var message = StaffingAlertRules.BuildMessage("Ola", "Kari Nordmann", Shift(eventName), openShifts: 1);

            Assert.Equal("Hei Ola! Kari Nordmann har trukket seg fra vakt tirsdag 14.10: 18–22 storheis. Oppgaven har nå 1 ledig vakt.", message);
        }

        [Fact]
        public void BuildMessage_UsesMinutes_OnlyWhenNotWholeHour()
        {
            var message = StaffingAlertRules.BuildMessage("Ola", "Kari Nordmann", Shift(startMinute: 30), openShifts: 1);

            Assert.Contains("tirsdag 14.10: 18:30–22 storheis (Diskokveld).", message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void BuildMessage_WithoutFirstName_GreetsWithoutName(string? firstName)
        {
            var message = StaffingAlertRules.BuildMessage(firstName, "Kari Nordmann", Shift(), openShifts: 1);

            Assert.StartsWith("Hei! Kari Nordmann har trukket seg", message);
        }

        [Fact]
        public void BuildMessage_FormatsDayAndShift_LikeShiftReminder()
        {
            var shift = Shift();

            var message = StaffingAlertRules.BuildMessage("Ola", "Kari Nordmann", shift, openShifts: 1);

            Assert.Contains($"{ShiftReminderRules.FormatDay(Tuesday)}: {ShiftReminderRules.FormatShift(shift)}.", message);
        }
    }
}
