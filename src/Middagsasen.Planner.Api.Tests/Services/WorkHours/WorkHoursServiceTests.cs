using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services;
using Middagsasen.Planner.Api.Services.WorkHours;
using NSubstitute;

namespace Middagsasen.Planner.Api.Tests.Services.WorkHours
{
    public class WorkHoursServiceTests
    {
        private const int OwnerId = 10;
        private const int AdminId = 99;

        private readonly IWorkHourRepository _repository = Substitute.For<IWorkHourRepository>();
        private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
        private readonly WorkHoursService _sut;

        public WorkHoursServiceTests()
        {
            _sut = new WorkHoursService(_repository, _currentUser);
        }

        private void LoginAs(int userId, bool isAdmin)
        {
            _currentUser.UserId.Returns(userId);
            _currentUser.IsAdmin.Returns(isAdmin);
        }

        private WorkHour Stub(int? status = null)
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

            await _sut.UpdateWorkHour(5, new UpdateWorkHourRequest { Description = "Etter", ApprovalStatus = 1 });

            await _repository.Received(1).SaveChangesAsync();
            Assert.Equal("Etter", wh.Description);
            Assert.Equal(1, wh.ApprovalStatus);
            Assert.Equal(AdminId, wh.ApprovedBy);
            Assert.Equal(AdminId, wh.ModifiedBy);
        }

        [Fact]
        public async Task Update_Locked_DoesNotSave()
        {
            LoginAs(OwnerId, false);
            Stub(status: 1);

            await Assert.ThrowsAsync<EntityLockedException>(() =>
                _sut.UpdateWorkHour(5, new UpdateWorkHourRequest { Description = "x" }));

            await _repository.DidNotReceive().SaveChangesAsync();
        }

        [Fact]
        public async Task Update_AdminContentOnLockedWithStatus_ThrowsLockedAndDoesNotSave()
        {
            LoginAs(AdminId, true);
            var wh = Stub(status: 2);

            await Assert.ThrowsAsync<EntityLockedException>(() =>
                _sut.UpdateWorkHour(5, new UpdateWorkHourRequest { Description = "x", ApprovalStatus = 1 }));

            await _repository.DidNotReceive().SaveChangesAsync();
            Assert.Equal("Før", wh.Description);
            Assert.Equal(2, wh.ApprovalStatus);
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
    }
}
