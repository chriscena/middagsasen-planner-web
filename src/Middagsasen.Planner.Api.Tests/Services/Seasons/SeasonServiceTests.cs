using Middagsasen.Planner.Api.Core;
using Middagsasen.Planner.Api.Services.Seasons;
using Middagsasen.Planner.Api.Tests.Infrastructure;

namespace Middagsasen.Planner.Api.Tests.Services.Seasons
{
    public class SeasonServiceTests
    {
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
    
        [Fact]
        public void GetSeasons_JustAfterMidnight1JulyOslo_NewSeasonIsCurrent()
        {
            // 2025-06-30T22:30Z = 1. juli 00:30 norsk sommertid
            var service = new SeasonService(new FakeTimeProvider(new DateTimeOffset(2025, 6, 30, 22, 30, 0, TimeSpan.Zero)));

            Assert.Equal(2025, service.GetSeasons().First(s => s.IsCurrent).StartYear);
        }

        [Fact]
        public void GetSeasons_JustBeforeMidnight1JulyOslo_PreviousSeasonIsCurrent()
        {
            // 2025-06-30T21:30Z = 30. juni 23:30 norsk sommertid
            var service = new SeasonService(new FakeTimeProvider(new DateTimeOffset(2025, 6, 30, 21, 30, 0, TimeSpan.Zero)));

            Assert.Equal(2024, service.GetSeasons().First(s => s.IsCurrent).StartYear);
        }
    }
}
