using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services;
using Middagsasen.Planner.Api.Services.WorkHours;
using Middagsasen.Planner.Api.Tests.Infrastructure;
using NSubstitute;

namespace Middagsasen.Planner.Api.Tests.Services.WorkHours
{
    public class WorkHoursServiceTests
    {
        private const int OwnerId = 10;
        private const int AdminId = 99;
        /// <summary>Fast «nå»: 1. oktober 2026 → inneværende sesong 2026, høyeste gyldige sesong 2027.</summary>
        private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
        private const int CurrentSeason = 2026;

        private readonly IWorkHourRepository _repository = Substitute.For<IWorkHourRepository>();
        private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
        private readonly WorkHoursService _sut;

        public WorkHoursServiceTests()
        {
            _sut = new WorkHoursService(_repository, _currentUser, new FakeTimeProvider(Now));
        }

        private void LoginAs(int userId, bool isAdmin)
        {
            _currentUser.UserId.Returns(userId);
            _currentUser.IsAdmin.Returns(isAdmin);
        }

        private WorkHour Stub(ApprovalStatus? status = null)
        {
            var wh = new WorkHour
            {
                WorkHourId = 5,
                UserId = OwnerId,
                StartTime = new DateTime(2026, 1, 1, 8, 0, 0),
                EndTime = new DateTime(2026, 1, 1, 10, 0, 0),
                Description = "Før",
                ApprovalStatus = status,
            };
            _repository.GetWorkHourById(5).Returns(wh);
            _repository.GetWorkHourByIdReadOnly(5).Returns(wh);
            return wh;
        }

        [Fact]
        public async Task Update_ContentAndStatus_SavesOnce()
        {
            LoginAs(AdminId, true);
            var wh = Stub();

            await _sut.UpdateWorkHour(5, new UpdateWorkHourRequest { Description = "Etter", ApprovalStatus = ApprovalStatus.Approved });

            await _repository.Received(1).SaveChangesAsync();
            Assert.Equal("Etter", wh.Description);
            Assert.Equal(ApprovalStatus.Approved, wh.ApprovalStatus);
            Assert.Equal(AdminId, wh.ApprovedBy);
            Assert.Equal(AdminId, wh.ModifiedBy);
        }

        [Fact]
        public async Task Update_Locked_DoesNotSave()
        {
            LoginAs(OwnerId, false);
            Stub(status: ApprovalStatus.Approved);

            await Assert.ThrowsAsync<EntityLockedException>(() =>
                _sut.UpdateWorkHour(5, new UpdateWorkHourRequest { Description = "x" }));

            await _repository.DidNotReceive().SaveChangesAsync();
        }

        [Fact]
        public async Task Update_AdminContentOnLockedWithStatus_ThrowsLockedAndDoesNotSave()
        {
            LoginAs(AdminId, true);
            var wh = Stub(status: ApprovalStatus.Rejected);

            await Assert.ThrowsAsync<EntityLockedException>(() =>
                _sut.UpdateWorkHour(5, new UpdateWorkHourRequest { Description = "x", ApprovalStatus = ApprovalStatus.Approved }));

            await _repository.DidNotReceive().SaveChangesAsync();
            Assert.Equal("Før", wh.Description);
            Assert.Equal(ApprovalStatus.Rejected, wh.ApprovalStatus);
        }

        [Fact]
        public async Task Create_IgnoresAnyClientOwner_UsesCurrentUser()
        {
            LoginAs(OwnerId, false);
            WorkHour? added = null;
            _repository.When(r => r.Add(Arg.Any<WorkHour>())).Do(c => added = c.Arg<WorkHour>());
            _repository.GetWorkHourByIdReadOnly(Arg.Any<int>()).Returns(_ => added);

            var result = await _sut.CreateWorkHour(new CreateWorkHourRequest { StartTime = DateTime.UtcNow });

            Assert.NotNull(added);
            Assert.Equal(OwnerId, added!.UserId);
            Assert.Equal(OwnerId, result.UserId);
        }

        #region Sesongfilter

        public static TheoryData<int> InvalidSeasons => new() { 9999, 0, -1, 2022, CurrentSeason + 2 };
        public static TheoryData<int> ValidSeasons => new() { 2023, CurrentSeason, CurrentSeason + 1 };

        [Theory]
        [MemberData(nameof(InvalidSeasons))]
        public async Task GetWorkHours_InvalidSeason_ThrowsInvalidOperation(int season)
        {
            LoginAs(AdminId, true);

            var ex = await Assert.ThrowsAsync<DomainValidationException>(() => _sut.GetWorkHours(null, null, season));
            Assert.Equal(WorkHoursService.InvalidSeasonMessage, ex.Message);
        }

        [Theory]
        [MemberData(nameof(InvalidSeasons))]
        public async Task GetWorkHoursByUser_InvalidSeason_ThrowsInvalidOperation(int season)
        {
            LoginAs(OwnerId, false);

            await Assert.ThrowsAsync<DomainValidationException>(() => _sut.GetWorkHoursByUser(OwnerId, null, season));
        }

        [Theory]
        [MemberData(nameof(InvalidSeasons))]
        public async Task GetWorkHoursSum_InvalidSeason_ThrowsInvalidOperation(int season)
        {
            LoginAs(OwnerId, false);

            await Assert.ThrowsAsync<DomainValidationException>(() => _sut.GetWorkHoursSum(OwnerId, season));
        }

        [Theory]
        [MemberData(nameof(ValidSeasons))]
        public async Task GetWorkHours_ValidSeasonBoundary_IsAccepted(int season)
        {
            LoginAs(AdminId, true);
            _repository.GetWorkHours(Arg.Any<int?>(), Arg.Any<ApprovalFilter>(), Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<int>(), Arg.Any<int>())
                .Returns(((IReadOnlyList<WorkHour>)Array.Empty<WorkHour>(), 0));

            var result = await _sut.GetWorkHours(null, null, season);

            Assert.Equal(0, result.TotalCount);
        }

        [Fact]
        public async Task GetWorkHoursSum_Season_PassesOsloMidnightAsUtcRange()
        {
            LoginAs(OwnerId, false);
            _repository.GetIntervals(Arg.Any<int?>(), Arg.Any<DateTime?>(), Arg.Any<DateTime?>())
                .Returns(Array.Empty<WorkHourInterval>());

            await _sut.GetWorkHoursSum(OwnerId, 2025);

            // 1. juli 00:00 norsk sommertid = 30. juni 22:00 UTC
            await _repository.Received(1).GetIntervals(OwnerId,
                new DateTime(2025, 6, 30, 22, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 30, 22, 0, 0, DateTimeKind.Utc));
        }

        [Fact]
        public async Task GetWorkHoursSumPerUser_UsesCurrentSeasonFromTimeProvider()
        {
            LoginAs(AdminId, true);
            _repository.GetIntervals(Arg.Any<int?>(), Arg.Any<DateTime?>(), Arg.Any<DateTime?>())
                .Returns(Array.Empty<WorkHourInterval>());

            await _sut.GetWorkHoursSumPerUser();

            await _repository.Received(1).GetIntervals(null,
                new DateTime(2026, 6, 30, 22, 0, 0, DateTimeKind.Utc),
                new DateTime(2027, 6, 30, 22, 0, 0, DateTimeKind.Utc));
        }

        #endregion
    }
}
