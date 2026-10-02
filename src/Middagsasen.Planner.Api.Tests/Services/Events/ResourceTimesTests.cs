using System.Globalization;
using Middagsasen.Planner.Api.Services.Events;

namespace Middagsasen.Planner.Api.Tests.Services.Events
{
    public class ResourceTimesTests
    {
        private static DateTime D(string value) => DateTime.ParseExact(value, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        private static TimeSpan T(string value) => TimeSpan.ParseExact(value, @"hh\:mm", CultureInfo.InvariantCulture);

        [Theory]
        // Vanlig dagvakt: vakt litt utenfor vaktlista på begge sider, samme dag
        [InlineData("2026-01-15 10:00", "2026-01-15 17:00", "09:30", "17:30", "2026-01-15 09:30", "2026-01-15 17:30")]
        // Vaktliste starter like over midnatt, vakt starter før midnatt dagen før
        [InlineData("2026-01-02 00:15", "2026-01-02 06:00", "23:45", "06:30", "2026-01-01 23:45", "2026-01-02 06:30")]
        // Vaktliste over midnatt, vakt helt på neste døgn
        [InlineData("2026-01-15 14:00", "2026-01-16 04:00", "02:30", "05:00", "2026-01-16 02:30", "2026-01-16 05:00")]
        // Nattvakt: start samme dag, slutt neste dag
        [InlineData("2026-01-15 22:00", "2026-01-16 06:00", "21:30", "06:30", "2026-01-15 21:30", "2026-01-16 06:30")]
        // Kort vakt etter midnatt i nattliste havner på neste døgn
        [InlineData("2026-01-15 22:00", "2026-01-16 06:00", "01:00", "03:00", "2026-01-16 01:00", "2026-01-16 03:00")]
        // Månedsskifte
        [InlineData("2026-01-31 22:00", "2026-02-01 06:00", "01:00", "03:00", "2026-02-01 01:00", "2026-02-01 03:00")]
        [InlineData("2026-03-01 00:15", "2026-03-01 06:00", "23:45", "06:30", "2026-02-28 23:45", "2026-03-01 06:30")]
        // Årsskifte
        [InlineData("2025-12-31 22:00", "2026-01-01 06:00", "21:30", "06:30", "2025-12-31 21:30", "2026-01-01 06:30")]
        [InlineData("2026-01-01 00:15", "2026-01-01 06:00", "23:45", "06:30", "2025-12-31 23:45", "2026-01-01 06:30")]
        public void Place_PutsResourceOnDayNearestTheEvent(string eventStart, string eventEnd, string startTime, string endTime, string expectedStart, string expectedEnd)
        {
            var (start, end) = ResourceTimes.Place(D(eventStart), D(eventEnd), T(startTime), T(endTime));

            Assert.Equal(D(expectedStart), start);
            Assert.Equal(D(expectedEnd), end);
        }

        [Fact]
        public void Place_NormalizesEventEndBeforeStart_ToNextDay()
        {
            // Vaktliste 22:00–06:00 sendt med samme dato på begge
            var (start, end) = ResourceTimes.Place(D("2026-01-15 22:00"), D("2026-01-15 06:00"), T("01:00"), T("03:00"));

            Assert.Equal(D("2026-01-16 01:00"), start);
            Assert.Equal(D("2026-01-16 03:00"), end);
        }

        [Fact]
        public void Place_PrefersSameDay_OnTie()
        {
            // 12 t fra start dagen før og 12 t etter slutt samme dag: d = 0 vinner
            var (start, _) = ResourceTimes.Place(D("2026-01-15 00:00"), D("2026-01-15 00:00"), T("12:00"), T("13:00"));

            Assert.Equal(D("2026-01-15 12:00"), start);
        }

        [Fact]
        public void Place_PrefersSameDay_WhenSeveralCandidatesAreInsideLongEvent()
        {
            // Vaktliste over 48 t: både 15. og 16. kl. 12:00 er innenfor — d = 0 vinner
            var (start, _) = ResourceTimes.Place(D("2026-01-15 06:00"), D("2026-01-17 06:00"), T("12:00"), T("13:00"));

            Assert.Equal(D("2026-01-15 12:00"), start);
        }

        [Fact]
        public void Place_PicksNextDay_WhenOnlyItIsInsideLongEvent()
        {
            // d = 0 (15. 05:00) er 1 t før start, d = +1 (16. 05:00) er innenfor
            var (start, end) = ResourceTimes.Place(D("2026-01-15 06:00"), D("2026-01-17 06:00"), T("05:00"), T("07:00"));

            Assert.Equal(D("2026-01-16 05:00"), start);
            Assert.Equal(D("2026-01-16 07:00"), end);
        }

        [Fact]
        public void Place_KeepsZeroLengthResource_OnSameMoment()
        {
            var (start, end) = ResourceTimes.Place(D("2026-01-15 10:00"), D("2026-01-15 17:00"), T("12:00"), T("12:00"));

            Assert.Equal(D("2026-01-15 12:00"), start);
            Assert.Equal(start, end);
        }

        [Theory]
        [InlineData("2026-01-15 22:00", "2026-01-15 06:00", "2026-01-16 06:00")]
        [InlineData("2026-01-15 10:00", "2026-01-15 17:00", "2026-01-15 17:00")]
        [InlineData("2026-01-15 10:00", "2026-01-17 17:00", "2026-01-17 17:00")] // over 24 t er urørt
        public void NormalizeEventEnd_AddsDayOnlyWhenEndIsBeforeStart(string eventStart, string eventEnd, string expected)
        {
            Assert.Equal(D(expected), ResourceTimes.NormalizeEventEnd(D(eventStart), D(eventEnd)));
        }
    }
}
