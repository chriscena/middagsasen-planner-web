using Middagsasen.Planner.Api.Services.Events;
using static Middagsasen.Planner.Api.Services.Events.FacilityRequirementRules;

namespace Middagsasen.Planner.Api.Tests.Services.Events
{
    public class FacilityRequirementRulesTests
    {
        private static DateTime At(int hour, int minute = 0) => new DateTime(2026, 1, 15).AddHours(hour).AddMinutes(minute);

        private static StaffedPeriod Shift(int userId, int startHour, int endHour) => new(userId, At(startHour), At(endHour));

        [Fact]
        public void IssueExample_SnowmobileDriverOnlyInKiosk17To19_GivesBreach19To21()
        {
            // Vaktliste 17–21; snøskuterføreren står i kiosken 17–19. Storheisvakta 18–21 har ikke kompetansen og er ikke med.
            var breaches = FindBreaches(At(17), At(21), 1, [Shift(1, 17, 19)]);

            var breach = Assert.Single(breaches);
            Assert.Equal(new Breach(At(19), At(21), 0), breach);
        }

        [Fact]
        public void RequirementMetAllTheTime_GivesNoBreaches()
        {
            var breaches = FindBreaches(At(17), At(21), 1, [Shift(1, 17, 19), Shift(2, 19, 21)]);

            Assert.Empty(breaches);
        }

        [Fact]
        public void NoShifts_GivesBreachForWholeOpeningHoursWithZero()
        {
            var breaches = FindBreaches(At(17), At(21), 1, []);

            Assert.Equal([new Breach(At(17), At(21), 0)], breaches);
        }

        [Fact]
        public void SameUserWithOverlappingShifts_CountsOnce()
        {
            var breaches = FindBreaches(At(17), At(21), 2, [Shift(1, 17, 21), Shift(1, 18, 20)]);

            Assert.Equal([new Breach(At(17), At(21), 1)], breaches);
        }

        [Fact]
        public void MinimumTwo_WithVaryingCount_GivesSeparateBreaches()
        {
            // 17–18: 0, 18–19: 1, 19–20: 2 (oppfylt), 20–21: 1.
            var breaches = FindBreaches(At(17), At(21), 2, [Shift(1, 18, 21), Shift(2, 19, 20)]);

            Assert.Equal(
                [new Breach(At(17), At(18), 0), new Breach(At(18), At(19), 1), new Breach(At(20), At(21), 1)],
                breaches);
        }

        [Fact]
        public void AdjacentSegmentsWithSameCount_AreMerged()
        {
            // Bruker 1 17–19 og bruker 2 19–21 gir ett i hele åpningstiden; segmentgrensen kl. 19 skal ikke synes.
            var breaches = FindBreaches(At(17), At(21), 2, [Shift(1, 17, 19), Shift(2, 19, 21)]);

            Assert.Equal([new Breach(At(17), At(21), 1)], breaches);
        }

        [Fact]
        public void ShiftsOutsideOpeningHours_AreClipped()
        {
            // 16–18 klippes til 17–18; 21–23 er helt utenfor.
            var breaches = FindBreaches(At(17), At(21), 1, [Shift(1, 16, 18), Shift(2, 21, 23)]);

            Assert.Equal([new Breach(At(18), At(21), 0)], breaches);
        }

        [Fact]
        public void ShiftCoveringMoreThanOpeningHours_MeetsRequirement()
        {
            var breaches = FindBreaches(At(17), At(21), 1, [Shift(1, 12, 23)]);

            Assert.Empty(breaches);
        }

        [Fact]
        public void HalfOpenIntervals_ShiftEndingWhenNextStarts_LeavesNoGap()
        {
            var breaches = FindBreaches(At(17), At(21), 1, [Shift(1, 17, 18), Shift(2, 18, 21)]);

            Assert.Empty(breaches);
        }

        [Fact]
        public void EmptyOpeningHours_GivesNoBreaches()
        {
            Assert.Empty(FindBreaches(At(17), At(17), 1, []));
        }

        [Fact]
        public void EffectivePeriod_UsesShiftTimes_WhenSet()
        {
            Assert.Equal((At(18), At(19, 30)), EffectivePeriod(At(18), At(19, 30), At(17), At(21)));
        }

        [Fact]
        public void EffectivePeriod_FallsBackToResourceTimes_WhenShiftTimesAreNull()
        {
            Assert.Equal((At(17), At(21)), EffectivePeriod(null, null, At(17), At(21)));
            Assert.Equal((At(18), At(21)), EffectivePeriod(At(18), null, At(17), At(21)));
            Assert.Equal((At(17), At(19)), EffectivePeriod(null, At(19), At(17), At(21)));
        }
    }
}
