using Middagsasen.Planner.Api.Core;
using Middagsasen.Planner.Api.Services.Seasons;

namespace Middagsasen.Planner.Api.Tests.Services.Seasons
{
    public class SeasonServiceTests
    {
        private sealed class FakeTimeProvider : TimeProvider
        {
            private readonly DateTimeOffset _now;
            public FakeTimeProvider(DateTimeOffset now) => _now = now;
            public override DateTimeOffset GetUtcNow() => _now;
        }

        private static SeasonService CreateService(int year, int month, int day)
            => new(new FakeTimeProvider(new DateTimeOffset(year, month, day, 12, 0, 0, TimeSpan.Zero)));

        [Fact]
        public void GetSeasons_ReturnsFromFirstSeasonToCurrent_Descending()
        {
            var seasons = CreateService(2026, 10, 1).GetSeasons().ToList();

            Assert.Equal(new[] { 2026, 2025, 2024, 2023 }, seasons.Select(s => s.StartYear));
            Assert.Equal(DateTimeExtensions.FirstSeasonStartYear, seasons.Last().StartYear);
        }

        [Fact]
        public void GetSeasons_OnlyFirstIsCurrent()
        {
            var seasons = CreateService(2026, 10, 1).GetSeasons().ToList();

            Assert.True(seasons[0].IsCurrent);
            Assert.All(seasons.Skip(1), s => Assert.False(s.IsCurrent));
        }

        [Fact]
        public void GetSeasons_HasCorrectLabels()
        {
            var seasons = CreateService(2025, 1, 15).GetSeasons().ToList();

            Assert.Equal(new[] { "2024/2025", "2023/2024" }, seasons.Select(s => s.Label));
        }

        [Fact]
        public void GetSeasons_On30June_CurrentIsPreviousStartYear()
        {
            var seasons = CreateService(2025, 6, 30).GetSeasons().ToList();

            Assert.Equal(2024, seasons[0].StartYear);
            Assert.True(seasons[0].IsCurrent);
        }

        [Fact]
        public void GetSeasons_On1July_NewSeasonIsCurrent()
        {
            var seasons = CreateService(2025, 7, 1).GetSeasons().ToList();

            Assert.Equal(2025, seasons[0].StartYear);
            Assert.True(seasons[0].IsCurrent);
            Assert.Equal(3, seasons.Count);
        }
    }
}
