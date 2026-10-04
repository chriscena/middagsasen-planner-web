using Middagsasen.Planner.Api.Services;
using Middagsasen.Planner.Api.Services.Shifts;

namespace Middagsasen.Planner.Api.Tests.Services.Shifts
{
    public class ShiftRulesTests
    {
        private const int OwnerId = 10;
        private const int OtherId = 20;
        private const int TrainerId = 30;
        private const int AdminId = 40;
        private const int ResourceTypeId = 5;

        private static readonly Actor Owner = new(OwnerId, false);
        private static readonly Actor Other = new(OtherId, false);
        private static readonly Actor Trainer = new(TrainerId, false);
        private static readonly Actor Admin = new(AdminId, true);

        private static readonly DateTime Start = new(2026, 1, 15, 9, 0, 0);
        private static readonly DateTime End = new(2026, 1, 15, 15, 0, 0);
        private static readonly DateTime Before = Start.AddDays(-1);
        private static readonly DateTime During = Start.AddHours(1);
        private static readonly DateTime AtEnd = End;

        private static ShiftFacts OwnerShift(bool needsTraining = false) => new(1, OwnerId, needsTraining);

        private static ResourceFacts Resource(int minimumStaff = 2, bool hasTrainers = true, params ShiftFacts[] shifts) => new(
            ResourceId: 100,
            ResourceTypeId: ResourceTypeId,
            StartTime: Start,
            EndTime: End,
            MinimumStaff: minimumStaff,
            HasTraining: hasTrainers,
            TrainerUserIds: hasTrainers ? new HashSet<int> { TrainerId } : new HashSet<int>(),
            Shifts: shifts);

        public enum Who { Admin, Owner, Trainer, Other }

        private static Actor Resolve(Who who) => who switch
        {
            Who.Admin => Admin,
            Who.Owner => Owner,
            Who.Trainer => Trainer,
            Who.Other => Other,
            _ => throw new ArgumentOutOfRangeException(nameof(who)),
        };

        // --- Ressursstatus ---

        [Theory]
        [InlineData(2, 0, true, false)]
        [InlineData(2, 1, true, false)]
        [InlineData(2, 2, false, true)]
        [InlineData(2, 3, false, true)]
        [InlineData(0, 0, false, true)]
        public void IsMissingStaff_And_IsFull(int minimumStaff, int shiftCount, bool missingStaff, bool full)
        {
            var shifts = Enumerable.Range(1, shiftCount).Select(i => new ShiftFacts(i, 1000 + i, false)).ToArray();
            var resource = Resource(minimumStaff, true, shifts);

            Assert.Equal(missingStaff, ShiftRules.IsMissingStaff(resource));
            Assert.Equal(full, ShiftRules.IsFull(resource));
            Assert.Equal(missingStaff, ShiftRules.IsMissingStaff(new ResourceStaffing(minimumStaff, shiftCount)));
        }

        // --- Ledige plasser ---

        [Theory]
        // Kolonner: minimum bemanning, antall vakter, forventet ny minimum bemanning
        [InlineData(3, 0, 4)]
        [InlineData(3, 2, 4)]
        [InlineData(3, 3, 4)]
        [InlineData(1, 3, 4)] // overbooket: én ledig plass utover vaktene
        [InlineData(0, 0, 1)]
        public void MinimumStaffAfterAddingEmptySlot(int minimumStaff, int shiftCount, int expected)
        {
            Assert.Equal(expected, ShiftRules.MinimumStaffAfterAddingEmptySlot(new ResourceStaffing(minimumStaff, shiftCount)));
        }

        [Theory]
        // Kolonner: minimum bemanning, antall vakter, forventet ny minimum bemanning (null = ingen ledig plass)
        [InlineData(3, 0, 2)]
        [InlineData(3, 2, 2)]
        [InlineData(1, 0, 0)]
        [InlineData(3, 3, null)]
        [InlineData(1, 3, null)]
        [InlineData(0, 0, null)]
        public void MinimumStaffAfterRemovingEmptySlot(int minimumStaff, int shiftCount, int? expected)
        {
            Assert.Equal(expected, ShiftRules.MinimumStaffAfterRemovingEmptySlot(new ResourceStaffing(minimumStaff, shiftCount)));
        }

        [Theory]
        // Kolonner: minimum bemanning, antall vakter, endring, forventet ny minimum bemanning (null = for få ledige plasser)
        [InlineData(5, 0, 0, 5)]
        [InlineData(5, 0, 1, 6)]
        [InlineData(1, 3, 2, 3)] // overbooket: økningen legges på MinimumStaff
        [InlineData(5, 2, -3, 2)] // alle tre ledige plasser fjernes
        [InlineData(5, 2, -4, null)] // ville fjernet en bemannet vakt
        [InlineData(3, 0, -3, 0)]
        [InlineData(3, 0, -4, null)] // under 0
        [InlineData(3, 3, -1, null)] // full
        [InlineData(1, 3, -1, null)] // overbooket
        [InlineData(3, 3, 0, 3)]
        public void MinimumStaffAfterChange(int minimumStaff, int shiftCount, int change, int? expected)
        {
            Assert.Equal(expected, ShiftRules.MinimumStaffAfterChange(new ResourceStaffing(minimumStaff, shiftCount), change));
        }

        [Theory]
        [InlineData(3, 1, 2)]
        [InlineData(3, 3, 0)]
        [InlineData(1, 3, 0)]
        public void EmptySlots(int minimumStaff, int shiftCount, int expected)
        {
            Assert.Equal(expected, ShiftRules.EmptySlots(new ResourceStaffing(minimumStaff, shiftCount)));
        }

        [Fact]
        public void IsPast_WhenEndTimeIsReached()
        {
            var resource = Resource();
            Assert.False(ShiftRules.IsPast(resource, Before));
            Assert.False(ShiftRules.IsPast(resource, During));
            Assert.True(ShiftRules.IsPast(resource, AtEnd));
            Assert.True(ShiftRules.IsPast(resource, AtEnd.AddMinutes(1)));
        }

        [Theory]
        [InlineData(true, false, true)]
        [InlineData(true, true, false)]
        [InlineData(false, false, false)]
        [InlineData(false, true, false)]
        public void MustAnswerTraining(bool hasTraining, bool userHasTraining, bool expected)
        {
            Assert.Equal(expected, ShiftRules.MustAnswerTraining(Resource(hasTrainers: hasTraining), userHasTraining));
        }

        // --- Ta vakt ---

        [Theory]
        // Kolonner: hvem, setter opp seg selv, forventet
        [InlineData(Who.Owner, true, null)]
        [InlineData(Who.Owner, false, ShiftRuleViolation.Forbidden)]
        [InlineData(Who.Trainer, false, ShiftRuleViolation.Forbidden)]
        [InlineData(Who.Admin, true, null)]
        [InlineData(Who.Admin, false, null)]
        public void CheckSignUp_Access(Who who, bool self, ShiftRuleViolation? expected)
        {
            var actor = Resolve(who);
            var target = self ? actor.UserId : OtherId;

            Assert.Equal(expected, ShiftRules.CheckSignUp(actor, Resource(), Before, target));
        }

        [Theory]
        [InlineData(Who.Owner, ShiftRuleViolation.Full)]
        [InlineData(Who.Trainer, ShiftRuleViolation.Full)]
        [InlineData(Who.Admin, null)]
        public void CheckSignUp_Capacity_OnlyAdminCanOverbook(Who who, ShiftRuleViolation? expected)
        {
            var actor = Resolve(who);
            var full = Resource(2, true, new ShiftFacts(1, 1001, false), new ShiftFacts(2, 1002, false));

            Assert.Equal(expected, ShiftRules.CheckSignUp(actor, full, Before, actor.UserId));
        }

        [Theory]
        [InlineData(Who.Owner, ShiftRuleViolation.Past)]
        [InlineData(Who.Trainer, ShiftRuleViolation.Past)]
        [InlineData(Who.Admin, null)]
        public void CheckSignUp_Past_OnlyAdmin(Who who, ShiftRuleViolation? expected)
        {
            var actor = Resolve(who);
            Assert.Equal(expected, ShiftRules.CheckSignUp(actor, Resource(), AtEnd, actor.UserId));
        }

        [Theory]
        [InlineData(Who.Owner)]
        [InlineData(Who.Admin)]
        public void CheckSignUp_Duplicate_AlsoForAdmin(Who who)
        {
            var actor = Resolve(who);
            var resource = Resource(5, true, new ShiftFacts(1, OwnerId, false));

            Assert.Equal(ShiftRuleViolation.Duplicate, ShiftRules.CheckSignUp(actor, resource, Before, OwnerId));
        }

        [Theory]
        // Kolonner: start-offset i timer fra ressursens start (null = ressursens), slutt-offset fra ressursens slutt (null = ressursens), gyldig
        [InlineData(null, null, true)]
        [InlineData(0, 0, true)]
        [InlineData(1, -1, true)]
        [InlineData(-1, null, false)]
        [InlineData(null, 1, false)]
        [InlineData(5, -5, false)] // start etter slutt
        public void CheckSignUp_TimesMustBeWithinResource(int? startOffset, int? endOffset, bool valid)
        {
            DateTime? start = startOffset is { } s ? Start.AddHours(s) : null;
            DateTime? end = endOffset is { } e ? End.AddHours(e) : null;

            var result = ShiftRules.CheckSignUp(Admin, Resource(), Before, OwnerId, start, end);

            Assert.Equal(valid ? null : ShiftRuleViolation.InvalidTimes, result);
        }

        [Fact]
        public void CanSignUp_MatchesCheckSignUp()
        {
            var empty = Resource();
            var full = Resource(1, true, new ShiftFacts(1, OtherId, false));
            var withOwner = Resource(5, true, new ShiftFacts(1, OwnerId, false));

            foreach (var actor in new[] { Owner, Trainer, Admin, Other })
            foreach (var resource in new[] { empty, full, withOwner })
            foreach (var now in new[] { Before, AtEnd })
            {
                Assert.Equal(ShiftRules.CheckSignUp(actor, resource, now, actor.UserId) is null, ShiftRules.CanSignUp(actor, resource, now));
            }

            Assert.True(ShiftRules.CanSignUp(Owner, empty, Before));
            Assert.False(ShiftRules.CanSignUp(Owner, full, Before));
            Assert.True(ShiftRules.CanSignUp(Admin, full, Before));
            Assert.False(ShiftRules.CanSignUp(Owner, withOwner, Before));
            Assert.False(ShiftRules.CanSignUp(Owner, empty, AtEnd));
            Assert.True(ShiftRules.CanSignUp(Admin, empty, AtEnd));
        }

        // --- Endre vakt ---

        [Theory]
        // Kolonner: hvem, flytter vakta, avsluttet, forventet
        [InlineData(Who.Admin, false, false, null)]
        [InlineData(Who.Admin, true, false, null)]
        [InlineData(Who.Admin, true, true, null)]
        [InlineData(Who.Owner, false, false, null)]
        [InlineData(Who.Owner, true, false, ShiftRuleViolation.Forbidden)]
        [InlineData(Who.Owner, false, true, ShiftRuleViolation.Past)]
        // Trenere bruker SetTraining og har ikke lenger tilgang til å endre vakta.
        [InlineData(Who.Trainer, false, false, ShiftRuleViolation.Forbidden)]
        [InlineData(Who.Other, false, false, ShiftRuleViolation.Forbidden)]
        public void CheckChange_Access(Who who, bool movesShift, bool past, ShiftRuleViolation? expected)
        {
            var shift = OwnerShift();
            var resource = Resource(2, true, shift);
            int? newUserId = movesShift ? OtherId : null;

            var result = ShiftRules.CheckChange(Resolve(who), resource, past ? AtEnd : Before, shift, newUserId);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void CheckChange_OwnerKeepingOwnId_IsNotAMove()
        {
            var shift = OwnerShift();
            Assert.Null(ShiftRules.CheckChange(Owner, Resource(2, true, shift), Before, shift, OwnerId));
        }

        [Fact]
        public void CheckChange_Admin_CannotMoveToUserAlreadyOnResource()
        {
            var shift = OwnerShift();
            var resource = Resource(5, true, shift, new ShiftFacts(2, OtherId, false));

            Assert.Equal(ShiftRuleViolation.Duplicate, ShiftRules.CheckChange(Admin, resource, Before, shift, OtherId));
        }

        [Fact]
        public void CheckChange_ValidatesTimesOnlyWhenTheyChange()
        {
            var shift = OwnerShift();
            var resource = Resource(2, true, shift);
            // Lagrede tider utenfor ressursen (ressursen er flyttet etter at vakta ble tatt).
            var storedStart = Start.AddHours(-2);

            Assert.Null(ShiftRules.CheckChange(Owner, resource, Before, shift, null, null, null, storedStart, End));
            Assert.Equal(ShiftRuleViolation.InvalidTimes,
                ShiftRules.CheckChange(Owner, resource, Before, shift, null, null, End.AddHours(-1), storedStart, End));
            Assert.Null(ShiftRules.CheckChange(Owner, resource, Before, shift, null, Start, End.AddHours(-1), storedStart, End));
            Assert.Equal(ShiftRuleViolation.InvalidTimes,
                ShiftRules.CheckChange(Owner, resource, Before, shift, null, End, null, Start, Start.AddHours(1)));
        }

        // --- Opplæring ---

        [Theory]
        [InlineData(Who.Admin, false, null)]
        [InlineData(Who.Admin, true, null)]
        [InlineData(Who.Owner, false, null)]
        [InlineData(Who.Owner, true, ShiftRuleViolation.Past)]
        [InlineData(Who.Trainer, false, null)]
        [InlineData(Who.Trainer, true, ShiftRuleViolation.Past)]
        [InlineData(Who.Other, false, ShiftRuleViolation.Forbidden)]
        [InlineData(Who.Other, true, ShiftRuleViolation.Forbidden)]
        public void CheckSetTraining(Who who, bool past, ShiftRuleViolation? expected)
        {
            var shift = OwnerShift();
            Assert.Equal(expected, ShiftRules.CheckSetTraining(Resolve(who), Resource(2, true, shift), past ? AtEnd : Before, shift));
        }

        [Fact]
        public void CheckSetTraining_TrainerForOtherResourceType_IsForbidden()
        {
            var shift = OwnerShift();
            Assert.Equal(ShiftRuleViolation.Forbidden, ShiftRules.CheckSetTraining(Trainer, Resource(2, false, shift), Before, shift));
        }

        [Theory]
        // Kolonner: hvem, eieren trenger opplæring, avsluttet, forventet
        [InlineData(Who.Trainer, true, false, true)]
        [InlineData(Who.Trainer, false, false, false)]
        [InlineData(Who.Trainer, true, true, false)]
        [InlineData(Who.Admin, true, false, true)]
        [InlineData(Who.Admin, true, true, true)]
        [InlineData(Who.Owner, true, false, false)] // eieren kan sette egen opplæring, men ikke «bekrefte» den
        [InlineData(Who.Other, true, false, false)]
        public void CanConfirmTraining(Who who, bool needsTraining, bool past, bool expected)
        {
            var shift = OwnerShift(needsTraining);
            var actor = Resolve(who);
            var resource = Resource(2, true, shift);
            var now = past ? AtEnd : Before;

            Assert.Equal(expected, ShiftRules.CanConfirmTraining(actor, resource, now, shift));
            // Flagget gir aldri lov til noe håndhevelsen avviser.
            if (expected) Assert.Null(ShiftRules.CheckSetTraining(actor, resource, now, shift));
        }

        // --- Trekke seg ---

        [Theory]
        [InlineData(Who.Admin, false, null)]
        [InlineData(Who.Admin, true, null)]
        [InlineData(Who.Owner, false, null)]
        [InlineData(Who.Owner, true, ShiftRuleViolation.Past)]
        [InlineData(Who.Trainer, false, ShiftRuleViolation.Forbidden)]
        [InlineData(Who.Other, false, ShiftRuleViolation.Forbidden)]
        public void CheckWithdraw(Who who, bool past, ShiftRuleViolation? expected)
        {
            var shift = OwnerShift();
            Assert.Equal(expected, ShiftRules.CheckWithdraw(Resolve(who), Resource(2, true, shift), past ? AtEnd : Before, shift));
        }

        // --- Flagg og håndhevelse stemmer overens ---

        [Fact]
        public void Flags_MatchEnforcement()
        {
            var shift = OwnerShift(needsTraining: true);
            var resource = Resource(2, true, shift);

            foreach (var who in Enum.GetValues<Who>())
            foreach (var now in new[] { Before, During, AtEnd })
            {
                var actor = Resolve(who);
                Assert.Equal(ShiftRules.CheckChange(actor, resource, now, shift) is null, ShiftRules.CanEdit(actor, resource, now, shift));
                Assert.Equal(ShiftRules.CheckWithdraw(actor, resource, now, shift) is null, ShiftRules.CanWithdraw(actor, resource, now, shift));
                if (ShiftRules.CanConfirmTraining(actor, resource, now, shift))
                    Assert.Null(ShiftRules.CheckSetTraining(actor, resource, now, shift));
            }
        }
    }
}
