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

        private static DateTime? N(string? value) => value is null ? null : D(value);

        [Theory]
        // Utvidet slutt 10–17 → 10–20: hel vakt følger, delvakt foran urørt, delvakt med forankret slutt følger
        [InlineData("2026-02-01 10:00", "2026-02-01 17:00", "2026-02-01 10:00", "2026-02-01 20:00", 0, "2026-02-01 10:00", "2026-02-01 17:00", "2026-02-01 10:00", "2026-02-01 20:00")]
        [InlineData("2026-02-01 10:00", "2026-02-01 17:00", "2026-02-01 10:00", "2026-02-01 20:00", 0, "2026-02-01 10:00", "2026-02-01 14:00", "2026-02-01 10:00", "2026-02-01 14:00")]
        [InlineData("2026-02-01 10:00", "2026-02-01 17:00", "2026-02-01 10:00", "2026-02-01 20:00", 0, "2026-02-01 14:00", "2026-02-01 17:00", "2026-02-01 14:00", "2026-02-01 20:00")]
        // Forskjøvet 10–17 → 12–19
        [InlineData("2026-02-01 10:00", "2026-02-01 17:00", "2026-02-01 12:00", "2026-02-01 19:00", 0, "2026-02-01 10:00", "2026-02-01 17:00", "2026-02-01 12:00", "2026-02-01 19:00")]
        [InlineData("2026-02-01 10:00", "2026-02-01 17:00", "2026-02-01 12:00", "2026-02-01 19:00", 0, "2026-02-01 10:00", "2026-02-01 14:00", "2026-02-01 12:00", "2026-02-01 14:00")]
        [InlineData("2026-02-01 10:00", "2026-02-01 17:00", "2026-02-01 12:00", "2026-02-01 19:00", 0, "2026-02-01 13:00", "2026-02-01 17:00", "2026-02-01 13:00", "2026-02-01 19:00")]
        // Vaktlista flyttet fra 1. til 3. feb: alle vakter flyttes 2 døgn
        [InlineData("2026-02-01 10:00", "2026-02-01 17:00", "2026-02-03 10:00", "2026-02-03 17:00", 2, "2026-02-01 10:00", "2026-02-01 17:00", "2026-02-03 10:00", "2026-02-03 17:00")]
        [InlineData("2026-02-01 10:00", "2026-02-01 17:00", "2026-02-03 10:00", "2026-02-03 17:00", 2, "2026-02-01 11:00", "2026-02-01 14:00", "2026-02-03 11:00", "2026-02-03 14:00")]
        [InlineData("2026-02-01 10:00", "2026-02-01 17:00", "2026-02-03 10:00", "2026-02-03 17:00", 2, "2026-02-01 14:00", "2026-02-01 17:00", "2026-02-03 14:00", "2026-02-03 17:00")]
        // Krymping 10–20 → 10–15: hel vakt og delvakt klippes; delvakt helt etter ny slutt får ressursens tider
        [InlineData("2026-02-01 10:00", "2026-02-01 20:00", "2026-02-01 10:00", "2026-02-01 15:00", 0, "2026-02-01 10:00", "2026-02-01 20:00", "2026-02-01 10:00", "2026-02-01 15:00")]
        [InlineData("2026-02-01 10:00", "2026-02-01 20:00", "2026-02-01 10:00", "2026-02-01 15:00", 0, "2026-02-01 12:00", "2026-02-01 18:00", "2026-02-01 12:00", "2026-02-01 15:00")]
        [InlineData("2026-02-01 10:00", "2026-02-01 20:00", "2026-02-01 10:00", "2026-02-01 15:00", 0, "2026-02-01 16:00", "2026-02-01 20:00", "2026-02-01 10:00", "2026-02-01 15:00")]
        // 10–14 når ressursen går 10–17 → 15–19: start forankret til 15, slutt 14 → helt utenfor → ressursens tider
        [InlineData("2026-02-01 10:00", "2026-02-01 17:00", "2026-02-01 15:00", "2026-02-01 19:00", 0, "2026-02-01 10:00", "2026-02-01 14:00", "2026-02-01 15:00", "2026-02-01 19:00")]
        // Over midnatt 18–02 flyttet 2 døgn: vakter før og etter midnatt havner på riktig døgn
        [InlineData("2026-02-01 18:00", "2026-02-02 02:00", "2026-02-03 18:00", "2026-02-04 02:00", 2, "2026-02-01 22:00", "2026-02-02 02:00", "2026-02-03 22:00", "2026-02-04 02:00")]
        [InlineData("2026-02-01 18:00", "2026-02-02 02:00", "2026-02-03 18:00", "2026-02-04 02:00", 2, "2026-02-02 00:00", "2026-02-02 01:00", "2026-02-04 00:00", "2026-02-04 01:00")]
        [InlineData("2026-02-01 18:00", "2026-02-02 02:00", "2026-02-03 18:00", "2026-02-04 02:00", 2, "2026-02-01 18:00", "2026-02-01 23:00", "2026-02-03 18:00", "2026-02-03 23:00")]
        // Over midnatt 18–02 utvidet til 18–04: forankret slutt følger til 04 neste døgn
        [InlineData("2026-02-01 18:00", "2026-02-02 02:00", "2026-02-01 18:00", "2026-02-02 04:00", 0, "2026-02-02 00:00", "2026-02-02 02:00", "2026-02-02 00:00", "2026-02-02 04:00")]
        // 18–02 krympet til 18–23: vakt etter midnatt havner helt utenfor → ressursens tider
        [InlineData("2026-02-01 18:00", "2026-02-02 02:00", "2026-02-01 18:00", "2026-02-01 23:00", 0, "2026-02-02 00:00", "2026-02-02 02:00", "2026-02-01 18:00", "2026-02-01 23:00")]
        // Ressurs som starter dagen før vaktlista (23:45) flyttes 1 døgn
        [InlineData("2026-02-01 23:45", "2026-02-02 06:00", "2026-02-02 23:45", "2026-02-03 06:00", 1, "2026-02-02 01:00", "2026-02-02 03:00", "2026-02-03 01:00", "2026-02-03 03:00")]
        // Vaktliste 1. feb 18–02 uendret, ressurs 23–02 → 00–02 (havner på 2. feb): ingen døgnforskyvning, delvakter beholder døgnet
        [InlineData("2026-02-01 23:00", "2026-02-02 02:00", "2026-02-02 00:00", "2026-02-02 02:00", 0, "2026-02-02 01:00", "2026-02-02 02:00", "2026-02-02 01:00", "2026-02-02 02:00")]
        [InlineData("2026-02-01 23:00", "2026-02-02 02:00", "2026-02-02 00:00", "2026-02-02 02:00", 0, "2026-02-01 23:30", "2026-02-02 01:30", "2026-02-02 00:00", "2026-02-02 01:30")]
        // Vaktliste flyttet fra 1. til 3. feb og ressursen samtidig endret 10–17 → 12–19
        [InlineData("2026-02-01 10:00", "2026-02-01 17:00", "2026-02-03 12:00", "2026-02-03 19:00", 2, "2026-02-01 10:00", "2026-02-01 14:00", "2026-02-03 12:00", "2026-02-03 14:00")]
        [InlineData("2026-02-01 10:00", "2026-02-01 17:00", "2026-02-03 12:00", "2026-02-03 19:00", 2, "2026-02-01 13:00", "2026-02-01 17:00", "2026-02-03 13:00", "2026-02-03 19:00")]
        [InlineData("2026-02-01 10:00", "2026-02-01 17:00", "2026-02-03 12:00", "2026-02-03 19:00", 2, "2026-02-01 11:00", "2026-02-01 16:00", "2026-02-03 12:00", "2026-02-03 16:00")]
        // Vaktliste over midnatt flyttet fra 1. til 3. feb og ressursen samtidig endret 23–02 → 00–02: delvakt etter midnatt havner 4. feb
        [InlineData("2026-02-01 23:00", "2026-02-02 02:00", "2026-02-04 00:00", "2026-02-04 02:00", 2, "2026-02-02 01:00", "2026-02-02 02:00", "2026-02-04 01:00", "2026-02-04 02:00")]
        [InlineData("2026-02-01 23:00", "2026-02-02 02:00", "2026-02-04 00:00", "2026-02-04 02:00", 2, "2026-02-01 23:30", "2026-02-02 01:30", "2026-02-04 00:00", "2026-02-04 01:30")]
        public void FollowResource_AdjustsShiftToResource(
            string oldStart, string oldEnd, string newStart, string newEnd, int dayShift,
            string shiftStart, string shiftEnd, string expectedStart, string expectedEnd)
        {
            var (start, end) = ResourceTimes.FollowResource(
                D(oldStart), D(oldEnd), D(newStart), D(newEnd), TimeSpan.FromDays(dayShift), D(shiftStart), D(shiftEnd));

            Assert.Equal(D(expectedStart), start);
            Assert.Equal(D(expectedEnd), end);
        }

        [Theory]
        // Helt utenfor fra før (f.eks. admin-satte tider): røres ikke når ressursen er uendret
        [InlineData("2026-02-01 08:00", "2026-02-01 09:00")]
        [InlineData("2026-02-01 12:00", "2026-02-01 14:00")]
        [InlineData(null, null)]
        public void FollowResource_UnchangedResource_LeavesShiftUntouched(string? shiftStart, string? shiftEnd)
        {
            var (start, end) = ResourceTimes.FollowResource(
                D("2026-02-01 10:00"), D("2026-02-01 17:00"), D("2026-02-01 10:00"), D("2026-02-01 17:00"), TimeSpan.Zero, N(shiftStart), N(shiftEnd));

            Assert.Equal(N(shiftStart), start);
            Assert.Equal(N(shiftEnd), end);
        }

        [Theory]
        // Begge null: følger allerede ressursen og forblir null
        [InlineData(null, null, null, null)]
        // Bare slutt satt: start tolkes som ressursens kant, slutt flyttes med døgnforskyvningen
        [InlineData(null, "2026-02-01 14:00", null, "2026-02-03 14:00")]
        // Bare start satt, slutt tolkes som ny slutt
        [InlineData("2026-02-01 12:00", null, "2026-02-03 12:00", null)]
        public void FollowResource_NullFields_StayNull_AndOtherFieldIsAdjusted(
            string? shiftStart, string? shiftEnd, string? expectedStart, string? expectedEnd)
        {
            var (start, end) = ResourceTimes.FollowResource(
                D("2026-02-01 10:00"), D("2026-02-01 17:00"), D("2026-02-03 10:00"), D("2026-02-03 17:00"), TimeSpan.FromDays(2), N(shiftStart), N(shiftEnd));

            Assert.Equal(N(expectedStart), start);
            Assert.Equal(N(expectedEnd), end);
        }

        [Fact]
        public void FollowResource_OnlyEndSet_OutsideAfterShrink_GetsResourceEnd()
        {
            // Ressurs 10–20 → 15–20; vakt (null)–14: start tolkes som 15, slutt 14 → helt utenfor → slutt blir 20
            var (start, end) = ResourceTimes.FollowResource(
                D("2026-02-01 10:00"), D("2026-02-01 20:00"), D("2026-02-01 15:00"), D("2026-02-01 20:00"), TimeSpan.Zero, null, D("2026-02-01 14:00"));

            Assert.Null(start);
            Assert.Equal(D("2026-02-01 20:00"), end);
        }

        [Theory]
        // Nullengde 12–12 når ressursen utvides 10–17 → 10–20: beholdes
        [InlineData("2026-02-01 10:00", "2026-02-01 17:00", "2026-02-01 10:00", "2026-02-01 20:00", 0, "2026-02-01 12:00", "2026-02-01 12:00", "2026-02-01 12:00", "2026-02-01 12:00")]
        // Nullengde 12–12 når vaktlista flyttes fra 1. til 3. feb: flyttes, fortsatt nullengde
        [InlineData("2026-02-01 10:00", "2026-02-01 17:00", "2026-02-03 10:00", "2026-02-03 17:00", 2, "2026-02-01 12:00", "2026-02-01 12:00", "2026-02-03 12:00", "2026-02-03 12:00")]
        // Nullengde 17–17 når ressursen krympes 10–20 → 10–17: ligger på ny kant, beholdes
        [InlineData("2026-02-01 10:00", "2026-02-01 20:00", "2026-02-01 10:00", "2026-02-01 17:00", 0, "2026-02-01 17:00", "2026-02-01 17:00", "2026-02-01 17:00", "2026-02-01 17:00")]
        // Nullengde 18–18 når ressursen krympes 10–20 → 10–17: utenfor ressursen → ressursens tider
        [InlineData("2026-02-01 10:00", "2026-02-01 20:00", "2026-02-01 10:00", "2026-02-01 17:00", 0, "2026-02-01 18:00", "2026-02-01 18:00", "2026-02-01 10:00", "2026-02-01 17:00")]
        // 10–14 når ressursen blir 10–17 → 14–17: klippet ned til null lengde → ressursens tider (bevisst valg)
        [InlineData("2026-02-01 10:00", "2026-02-01 17:00", "2026-02-01 14:00", "2026-02-01 17:00", 0, "2026-02-01 10:00", "2026-02-01 14:00", "2026-02-01 14:00", "2026-02-01 17:00")]
        // 12–14 når ressursen blir 10–17 → 14–17: klippet ned til null lengde → ressursens tider
        [InlineData("2026-02-01 10:00", "2026-02-01 17:00", "2026-02-01 14:00", "2026-02-01 17:00", 0, "2026-02-01 12:00", "2026-02-01 14:00", "2026-02-01 14:00", "2026-02-01 17:00")]
        public void FollowResource_ZeroLength_KeptOnlyIfZeroLengthBefore(
            string oldStart, string oldEnd, string newStart, string newEnd, int dayShift,
            string shiftStart, string shiftEnd, string expectedStart, string expectedEnd)
        {
            var (start, end) = ResourceTimes.FollowResource(
                D(oldStart), D(oldEnd), D(newStart), D(newEnd), TimeSpan.FromDays(dayShift), D(shiftStart), D(shiftEnd));

            Assert.Equal(D(expectedStart), start);
            Assert.Equal(D(expectedEnd), end);
        }

        [Theory]
        // Vaktlista flyttet 2 døgn, samme klokkeslett
        [InlineData("2026-02-01 18:00", "2026-02-03 18:00", 2)]
        // Flyttet 2 døgn og samtidig ny starttid
        [InlineData("2026-02-01 10:00", "2026-02-03 12:00", 2)]
        // Starttid endret over midnatt bakover: ikke flyttet
        [InlineData("2026-02-01 00:30", "2026-01-31 23:30", 0)]
        // Starttid endret over midnatt forover: ikke flyttet
        [InlineData("2026-02-01 23:00", "2026-02-02 00:30", 0)]
        // Uendret
        [InlineData("2026-02-01 10:00", "2026-02-01 10:00", 0)]
        // Nøyaktig ±12 timer rundes bort fra null: ±1 døgn
        [InlineData("2026-02-01 06:00", "2026-02-01 18:00", 1)]
        [InlineData("2026-02-01 18:00", "2026-02-01 06:00", -1)]
        // Litt under 12 timer: 0
        [InlineData("2026-02-01 06:00", "2026-02-01 17:59", 0)]
        public void DayShift_RoundsEventMoveToNearestWholeDay(string oldEventStart, string newEventStart, int expectedDays)
        {
            Assert.Equal(TimeSpan.FromDays(expectedDays), ResourceTimes.DayShift(D(oldEventStart), D(newEventStart)));
        }

        [Fact]
        public void FollowResource_EventStartCrossesMidnightBackwards_ResourceUnchanged_LeavesShiftUntouched()
        {
            // Vaktliste 1. feb 00:30–06:00 → 31. jan 23:30–06:00; ressurs 01:00–05:00 uendret; delvakt 02:00–03:00
            var dayShift = ResourceTimes.DayShift(D("2026-02-01 00:30"), D("2026-01-31 23:30"));
            var (start, end) = ResourceTimes.FollowResource(
                D("2026-02-01 01:00"), D("2026-02-01 05:00"), D("2026-02-01 01:00"), D("2026-02-01 05:00"), dayShift,
                D("2026-02-01 02:00"), D("2026-02-01 03:00"));

            Assert.Equal(TimeSpan.Zero, dayShift);
            Assert.Equal(D("2026-02-01 02:00"), start);
            Assert.Equal(D("2026-02-01 03:00"), end);
        }

        [Fact]
        public void FollowResource_EventStartCrossesMidnightBackwards_ResourceChanged_ShiftKeepsDay()
        {
            // Samme vaktliste, men ressursen endres 01:00–05:00 → 01:00–06:00: delvakten 02:00–03:00 skal fortsatt ligge 1. feb
            var dayShift = ResourceTimes.DayShift(D("2026-02-01 00:30"), D("2026-01-31 23:30"));
            var (start, end) = ResourceTimes.FollowResource(
                D("2026-02-01 01:00"), D("2026-02-01 05:00"), D("2026-02-01 01:00"), D("2026-02-01 06:00"), dayShift,
                D("2026-02-01 02:00"), D("2026-02-01 03:00"));

            Assert.Equal(D("2026-02-01 02:00"), start);
            Assert.Equal(D("2026-02-01 03:00"), end);
        }
    }
}
