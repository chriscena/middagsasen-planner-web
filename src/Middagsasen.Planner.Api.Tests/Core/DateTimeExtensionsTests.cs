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
        public void GetSeasonRange_ReturnsHalfOpenIntervalFromJulyToJuly()
        {
            var (from, to) = DateTimeExtensions.GetSeasonRange(2024);

            Assert.Equal(new DateTime(2024, 7, 1), from);
            Assert.Equal(new DateTime(2025, 7, 1), to);
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
