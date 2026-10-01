using Middagsasen.Planner.Api.Core;

namespace Middagsasen.Planner.Api.Tests.Core
{
    public class DateTimeExtensionsTests
    {
        [Theory]
        [InlineData(2025, 6, 30, 2024)]
        [InlineData(2025, 7, 1, 2025)]
        [InlineData(2025, 1, 1, 2024)]
        [InlineData(2025, 12, 31, 2025)]
        public void GetSeasonStartYear_ReturnsYearSeasonStartedIn(int year, int month, int day, int expected)
        {
            Assert.Equal(expected, new DateTime(year, month, day, 23, 59, 59).GetSeasonStartYear());
        }

        [Fact]
        public void GetSeasonRangeUtc_ReturnsHalfOpenIntervalFromMidnight1JulyOsloInUtc()
        {
            var (from, to) = DateTimeExtensions.GetSeasonRangeUtc(2024);

            // 1. juli 00:00 norsk sommertid (UTC+2) = 30. juni 22:00 UTC
            Assert.Equal(new DateTime(2024, 6, 30, 22, 0, 0, DateTimeKind.Utc), from);
            Assert.Equal(new DateTime(2025, 6, 30, 22, 0, 0, DateTimeKind.Utc), to);
            Assert.Equal(DateTimeKind.Utc, from.Kind);
            Assert.Equal(DateTimeKind.Utc, to.Kind);
        }

        [Theory]
        [InlineData(2025, 6, 30, 22, 30, 2025)] // 1. juli 00:30 i Oslo
        [InlineData(2025, 6, 30, 21, 30, 2024)] // 30. juni 23:30 i Oslo
        [InlineData(2025, 6, 30, 22, 0, 2025)]  // nøyaktig sesongstart
        [InlineData(2026, 1, 15, 12, 0, 2025)]
        public void GetSeasonStartYear_ForInstant_IsEvaluatedInOsloTime(int year, int month, int day, int hour, int minute, int expected)
        {
            var instant = new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.Zero);

            Assert.Equal(expected, instant.GetSeasonStartYear());
        }

        [Fact]
        public void GetSeasonStartYear_ForCalendarDate_IgnoresTimeZone()
        {
            // Kalenderlogikk: 30. juni 23:30 er fortsatt forrige sesong uavhengig av Kind.
            Assert.Equal(2024, new DateTime(2025, 6, 30, 23, 30, 0, DateTimeKind.Utc).GetSeasonStartYear());
            Assert.Equal(2025, new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc).GetSeasonStartYear());
        }

        [Fact]
        public void ToSeasonLabel_ReturnsStartAndEndYear()
        {
            Assert.Equal("2024/2025", DateTimeExtensions.ToSeasonLabel(2024));
        }

        [Fact]
        public void ToSeason_UsesSeasonStartYear()
        {
            DateTime? june = new DateTime(2025, 6, 30);
            DateTime? july = new DateTime(2025, 7, 1);
            DateTime? none = null;

            Assert.Equal("2024/2025", june.ToSeason());
            Assert.Equal("2025/2026", july.ToSeason());
            Assert.Equal("", none.ToSeason());
        }
    }
}
