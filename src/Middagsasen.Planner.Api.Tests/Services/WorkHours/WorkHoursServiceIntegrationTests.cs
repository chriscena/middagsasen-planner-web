using Microsoft.EntityFrameworkCore;
using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services;
using Middagsasen.Planner.Api.Services.WorkHours;
using Middagsasen.Planner.Api.Tests.Infrastructure;
using NSubstitute;

namespace Middagsasen.Planner.Api.Tests.Services.WorkHours
{
    [Collection("Database")]
    public class WorkHoursServiceIntegrationTests
    {
        private readonly DatabaseFixture _fixture;

        private static readonly DateTime Start = new(2026, 1, 10, 9, 0, 0);
        private static readonly DateTime End = new(2026, 1, 10, 12, 0, 0);

        public WorkHoursServiceIntegrationTests(DatabaseFixture fixture)
        {
            _fixture = fixture;
        }

        private static ICurrentUserService MockCurrentUser(int userId, bool isAdmin)
        {
            var mock = Substitute.For<ICurrentUserService>();
            mock.UserId.Returns(userId);
            mock.IsAdmin.Returns(isAdmin);
            return mock;
        }

        private static WorkHoursService CreateService(PlannerDbContext context, User user)
            => new(new WorkHourRepository(context), MockCurrentUser(user.UserId, user.IsAdmin));

        private async Task<User> SeedUser(bool isAdmin = false, string firstName = "Test", string lastName = "User")
        {
            using var context = _fixture.CreateContext();
            var user = new User
            {
                UserName = $"+47{Random.Shared.Next(10000000, 99999999)}",
                FirstName = firstName,
                LastName = lastName,
                IsAdmin = isAdmin,
                Created = DateTime.UtcNow,
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();
            return user;
        }

        private async Task<WorkHour> SeedWorkHour(User owner, int? status = null, User? approvedBy = null, string? description = "Opprinnelig")
        {
            using var context = _fixture.CreateContext();
            var workHour = new WorkHour
            {
                UserId = owner.UserId,
                StartTime = Start,
                EndTime = End,
                Description = description,
                ApprovalStatus = status,
                ApprovedBy = approvedBy?.UserId,
                ApprovedTime = approvedBy != null ? DateTime.UtcNow.AddDays(-1) : null,
            };
            context.WorkHours.Add(workHour);
            await context.SaveChangesAsync();
            return workHour;
        }

        private async Task<WorkHour> Reload(int id)
        {
            using var context = _fixture.CreateContext();
            return await context.WorkHours.AsNoTracking().SingleAsync(w => w.WorkHourId == id);
        }

        #region Create

        [Fact]
        public async Task Create_UsesCurrentUserAsOwner()
        {
            var user = await SeedUser();
            using var context = _fixture.CreateContext();
            var service = CreateService(context, user);

            var result = await service.CreateWorkHour(new CreateWorkHourRequest { StartTime = Start, EndTime = End, Description = "Ny" });

            Assert.Equal(user.UserId, result.UserId);
            Assert.Equal(3.0m, result.Hours);
            Assert.Null(result.ApprovalStatus);
            Assert.Null(result.ModifiedBy);
            var db = await Reload(result.WorkHourId);
            Assert.Equal(user.UserId, db.UserId);
            Assert.Equal("Ny", db.Description);
        }

        [Fact]
        public async Task Create_WithoutStartTime_ThrowsInvalidOperation()
        {
            var user = await SeedUser();
            using var context = _fixture.CreateContext();
            var service = CreateService(context, user);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.CreateWorkHour(new CreateWorkHourRequest { EndTime = End }));
        }

        #endregion

        #region Update (PATCH)

        [Fact]
        public async Task Update_Partial_OnlyChangesSentFields()
        {
            var owner = await SeedUser();
            var wh = await SeedWorkHour(owner);
            using var context = _fixture.CreateContext();
            var service = CreateService(context, owner);

            var result = await service.UpdateWorkHour(wh.WorkHourId, new UpdateWorkHourRequest { Description = "Ny beskrivelse" });

            Assert.Equal("Ny beskrivelse", result.Description);
            var db = await Reload(wh.WorkHourId);
            Assert.Equal("Ny beskrivelse", db.Description);
            Assert.Equal(Start, db.StartTime);
            Assert.Equal(End, db.EndTime);
            Assert.Null(db.ApprovalStatus);
        }

        [Fact]
        public async Task Update_PartialEndTime_KeepsDescriptionAndStart()
        {
            var owner = await SeedUser();
            var wh = await SeedWorkHour(owner);
            using var context = _fixture.CreateContext();
            var service = CreateService(context, owner);

            var newEnd = End.AddHours(1);
            var result = await service.UpdateWorkHour(wh.WorkHourId, new UpdateWorkHourRequest { EndTime = newEnd });

            Assert.Equal(4.0m, result.Hours);
            var db = await Reload(wh.WorkHourId);
            Assert.Equal(newEnd, db.EndTime);
            Assert.Equal(Start, db.StartTime);
            Assert.Equal("Opprinnelig", db.Description);
        }

        [Fact]
        public async Task Update_ByOwner_DoesNotSetModifiedBy()
        {
            var owner = await SeedUser();
            var wh = await SeedWorkHour(owner);
            using var context = _fixture.CreateContext();
            var service = CreateService(context, owner);

            var result = await service.UpdateWorkHour(wh.WorkHourId, new UpdateWorkHourRequest { Description = "Endret av eier" });

            Assert.Null(result.ModifiedBy);
            Assert.Null(result.ModifiedTime);
            Assert.Null((await Reload(wh.WorkHourId)).ModifiedBy);
        }

        [Fact]
        public async Task Update_ByAdminNotOwner_SetsModifiedByAndName()
        {
            var owner = await SeedUser();
            var admin = await SeedUser(isAdmin: true, firstName: "Ada", lastName: "Admin");
            var wh = await SeedWorkHour(owner);
            using var context = _fixture.CreateContext();
            var service = CreateService(context, admin);

            var before = DateTime.UtcNow.AddMinutes(-1);
            var result = await service.UpdateWorkHour(wh.WorkHourId, new UpdateWorkHourRequest { StartTime = Start.AddMinutes(30) });

            Assert.Equal(admin.UserId, result.ModifiedBy);
            Assert.Equal("Ada Admin", result.ModifiedByName);
            Assert.NotNull(result.ModifiedTime);
            Assert.True(result.ModifiedTime > before);
            Assert.Equal(owner.UserId, result.UserId);
            var db = await Reload(wh.WorkHourId);
            Assert.Equal(admin.UserId, db.ModifiedBy);
            Assert.Equal(owner.UserId, db.UserId);
        }

        [Fact]
        public async Task Update_ByAdmin_WithUnchangedValues_DoesNotSetModifiedBy()
        {
            var owner = await SeedUser();
            var admin = await SeedUser(isAdmin: true);
            var wh = await SeedWorkHour(owner);
            using var context = _fixture.CreateContext();
            var service = CreateService(context, admin);

            var result = await service.UpdateWorkHour(wh.WorkHourId, new UpdateWorkHourRequest
            {
                StartTime = Start,
                EndTime = End,
                Description = "Opprinnelig",
            });

            Assert.Null(result.ModifiedBy);
            Assert.Null((await Reload(wh.WorkHourId)).ModifiedBy);
        }

        [Fact]
        public async Task Update_ByAdmin_OnlyStatus_DoesNotSetModifiedBy()
        {
            var owner = await SeedUser();
            var admin = await SeedUser(isAdmin: true);
            var wh = await SeedWorkHour(owner);
            using var context = _fixture.CreateContext();
            var service = CreateService(context, admin);

            var result = await service.UpdateWorkHour(wh.WorkHourId, new UpdateWorkHourRequest { ApprovalStatus = 1 });

            Assert.Equal(1, result.ApprovalStatus);
            Assert.Null(result.ModifiedBy);
            Assert.Null((await Reload(wh.WorkHourId)).ModifiedBy);
        }

        [Fact]
        public async Task Update_OwnerEditsAfterAdmin_KeepsModifiedBy()
        {
            var owner = await SeedUser();
            var admin = await SeedUser(isAdmin: true);
            var wh = await SeedWorkHour(owner);

            using (var adminContext = _fixture.CreateContext())
            {
                await CreateService(adminContext, admin)
                    .UpdateWorkHour(wh.WorkHourId, new UpdateWorkHourRequest { Description = "Admin" });
            }

            using var ownerContext = _fixture.CreateContext();
            var result = await CreateService(ownerContext, owner)
                .UpdateWorkHour(wh.WorkHourId, new UpdateWorkHourRequest { Description = "Eier igjen" });

            Assert.Equal("Eier igjen", result.Description);
            Assert.Equal(admin.UserId, result.ModifiedBy);
            Assert.Equal(admin.UserId, (await Reload(wh.WorkHourId)).ModifiedBy);
        }

        [Fact]
        public async Task Update_ContentAndApproval_SavedTogether_ApprovedByFromCurrentUser()
        {
            var owner = await SeedUser();
            var admin = await SeedUser(isAdmin: true, firstName: "Gunn", lastName: "Godkjenner");
            var wh = await SeedWorkHour(owner);
            using var context = _fixture.CreateContext();
            var service = CreateService(context, admin);

            var before = DateTime.UtcNow.AddMinutes(-1);
            var result = await service.UpdateWorkHour(wh.WorkHourId, new UpdateWorkHourRequest
            {
                Description = "Rettet",
                ApprovalStatus = 1,
            });

            Assert.Equal("Rettet", result.Description);
            Assert.Equal(1, result.ApprovalStatus);
            Assert.Equal(admin.UserId, result.ApprovedBy);
            Assert.Equal("Gunn Godkjenner", result.ApprovedByName);
            Assert.Equal(admin.UserId, result.ModifiedBy);
            Assert.True(result.ApprovedTime > before);

            var db = await Reload(wh.WorkHourId);
            Assert.Equal("Rettet", db.Description);
            Assert.Equal(1, db.ApprovalStatus);
            Assert.Equal(admin.UserId, db.ApprovedBy);
            Assert.NotNull(db.ApprovedTime);
        }

        [Fact]
        public async Task Update_ResetStatusToNullViaPatch_IsNotPossible_NullMeansAbsent()
        {
            var owner = await SeedUser();
            var admin = await SeedUser(isAdmin: true);
            var wh = await SeedWorkHour(owner, status: 1, approvedBy: admin);
            using var context = _fixture.CreateContext();
            var service = CreateService(context, admin);

            // Tom PATCH (alle felter null) endrer ingenting.
            var result = await service.UpdateWorkHour(wh.WorkHourId, new UpdateWorkHourRequest());

            Assert.Equal(1, result.ApprovalStatus);
            Assert.Equal(1, (await Reload(wh.WorkHourId)).ApprovalStatus);
        }

        [Fact]
        public async Task Update_InvalidStatus_ThrowsInvalidOperation()
        {
            var owner = await SeedUser();
            var admin = await SeedUser(isAdmin: true);
            var wh = await SeedWorkHour(owner);
            using var context = _fixture.CreateContext();
            var service = CreateService(context, admin);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.UpdateWorkHour(wh.WorkHourId, new UpdateWorkHourRequest { ApprovalStatus = 3 }));
        }

        [Fact]
        public async Task Update_NotFound_ThrowsEntityNotFound()
        {
            var owner = await SeedUser();
            using var context = _fixture.CreateContext();
            var service = CreateService(context, owner);

            await Assert.ThrowsAsync<EntityNotFoundException>(() =>
                service.UpdateWorkHour(int.MaxValue, new UpdateWorkHourRequest { Description = "x" }));
        }

        [Fact]
        public async Task Update_ByOtherNonAdmin_ThrowsForbidden()
        {
            var owner = await SeedUser();
            var other = await SeedUser();
            var wh = await SeedWorkHour(owner);
            using var context = _fixture.CreateContext();
            var service = CreateService(context, other);

            await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
                service.UpdateWorkHour(wh.WorkHourId, new UpdateWorkHourRequest { Description = "x" }));
            Assert.Equal("Opprinnelig", (await Reload(wh.WorkHourId)).Description);
        }

        [Fact]
        public async Task Update_ByOtherNonAdmin_OnLockedEntry_ThrowsForbiddenNotLocked()
        {
            var owner = await SeedUser();
            var other = await SeedUser();
            var admin = await SeedUser(isAdmin: true);
            var wh = await SeedWorkHour(owner, status: 1, approvedBy: admin);
            using var context = _fixture.CreateContext();
            var service = CreateService(context, other);

            await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
                service.UpdateWorkHour(wh.WorkHourId, new UpdateWorkHourRequest { Description = "x" }));
        }

        [Fact]
        public async Task Update_OwnerSetsStatus_ThrowsForbidden()
        {
            var owner = await SeedUser();
            var wh = await SeedWorkHour(owner);
            using var context = _fixture.CreateContext();
            var service = CreateService(context, owner);

            await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
                service.UpdateWorkHour(wh.WorkHourId, new UpdateWorkHourRequest { Description = "x", ApprovalStatus = 1 }));
            var db = await Reload(wh.WorkHourId);
            Assert.Equal("Opprinnelig", db.Description);
            Assert.Null(db.ApprovalStatus);
        }

        [Theory]
        [InlineData(false, 1)]
        [InlineData(false, 2)]
        [InlineData(true, 1)]
        [InlineData(true, 2)]
        public async Task Update_LockedEntry_OwnerOrAdmin_ThrowsLocked(bool asAdmin, int status)
        {
            var owner = await SeedUser();
            var admin = await SeedUser(isAdmin: true);
            var wh = await SeedWorkHour(owner, status: status, approvedBy: admin);
            using var context = _fixture.CreateContext();
            var service = CreateService(context, asAdmin ? admin : owner);

            await Assert.ThrowsAsync<EntityLockedException>(() =>
                service.UpdateWorkHour(wh.WorkHourId, new UpdateWorkHourRequest { Description = "x" }));
            Assert.Equal("Opprinnelig", (await Reload(wh.WorkHourId)).Description);
        }

        [Fact]
        public async Task Update_AdminApprovesAlreadyApproved_ThrowsLocked()
        {
            var owner = await SeedUser();
            var admin = await SeedUser(isAdmin: true);
            var wh = await SeedWorkHour(owner, status: 2, approvedBy: admin);
            using var context = _fixture.CreateContext();
            var service = CreateService(context, admin);

            await Assert.ThrowsAsync<EntityLockedException>(() =>
                service.UpdateWorkHour(wh.WorkHourId, new UpdateWorkHourRequest { ApprovalStatus = 1 }));
            Assert.Equal(2, (await Reload(wh.WorkHourId)).ApprovalStatus);
        }

        #endregion

        #region UpdateApprovedBy

        [Fact]
        public async Task UpdateApprovedBy_Approve_SetsApprovedByFromCurrentUser()
        {
            var owner = await SeedUser();
            var admin = await SeedUser(isAdmin: true);
            var wh = await SeedWorkHour(owner);
            using var context = _fixture.CreateContext();
            var service = CreateService(context, admin);

            var before = DateTime.UtcNow.AddMinutes(-1);
            var result = await service.UpdateApprovedBy(wh.WorkHourId, new ApprovedByRequest { ApprovalStatus = 2 });

            Assert.Equal(2, result.ApprovalStatus);
            Assert.Equal(admin.UserId, result.ApprovedBy);
            Assert.True(result.ApprovedTime > before);
            var db = await Reload(wh.WorkHourId);
            Assert.Equal(2, db.ApprovalStatus);
            Assert.Equal(admin.UserId, db.ApprovedBy);
            Assert.Null(db.ModifiedBy);
        }

        [Fact]
        public async Task UpdateApprovedBy_ResetToNull_ClearsApprovedByAndTime()
        {
            var owner = await SeedUser();
            var admin = await SeedUser(isAdmin: true);
            var wh = await SeedWorkHour(owner, status: 1, approvedBy: admin);
            using var context = _fixture.CreateContext();
            var service = CreateService(context, admin);

            var result = await service.UpdateApprovedBy(wh.WorkHourId, new ApprovedByRequest { ApprovalStatus = null });

            Assert.Null(result.ApprovalStatus);
            Assert.Null(result.ApprovedBy);
            Assert.Null(result.ApprovedTime);
            var db = await Reload(wh.WorkHourId);
            Assert.Null(db.ApprovalStatus);
            Assert.Null(db.ApprovedBy);
            Assert.Null(db.ApprovedTime);
        }

        [Fact]
        public async Task UpdateApprovedBy_ResetOnOpenEntry_ThrowsLocked()
        {
            var owner = await SeedUser();
            var admin = await SeedUser(isAdmin: true);
            var wh = await SeedWorkHour(owner);
            using var context = _fixture.CreateContext();
            var service = CreateService(context, admin);

            await Assert.ThrowsAsync<EntityLockedException>(() =>
                service.UpdateApprovedBy(wh.WorkHourId, new ApprovedByRequest { ApprovalStatus = null }));
        }

        [Fact]
        public async Task UpdateApprovedBy_NonAdminOwner_ThrowsForbidden()
        {
            var owner = await SeedUser();
            var wh = await SeedWorkHour(owner);
            using var context = _fixture.CreateContext();
            var service = CreateService(context, owner);

            await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
                service.UpdateApprovedBy(wh.WorkHourId, new ApprovedByRequest { ApprovalStatus = 1 }));
            Assert.Null((await Reload(wh.WorkHourId)).ApprovalStatus);
        }

        [Fact]
        public async Task UpdateApprovedBy_NotFound_ThrowsEntityNotFound()
        {
            var admin = await SeedUser(isAdmin: true);
            using var context = _fixture.CreateContext();
            var service = CreateService(context, admin);

            await Assert.ThrowsAsync<EntityNotFoundException>(() =>
                service.UpdateApprovedBy(int.MaxValue, new ApprovedByRequest { ApprovalStatus = 1 }));
        }

        #endregion

        #region Delete

        [Fact]
        public async Task Delete_OwnerOpenEntry_Deletes()
        {
            var owner = await SeedUser();
            var wh = await SeedWorkHour(owner);
            using var context = _fixture.CreateContext();
            var service = CreateService(context, owner);

            var result = await service.DeleteWorkHour(wh.WorkHourId);

            Assert.Equal(wh.WorkHourId, result.WorkHourId);
            using var verify = _fixture.CreateContext();
            Assert.False(await verify.WorkHours.AnyAsync(w => w.WorkHourId == wh.WorkHourId));
        }

        [Fact]
        public async Task Delete_AdminOpenEntry_Deletes()
        {
            var owner = await SeedUser();
            var admin = await SeedUser(isAdmin: true);
            var wh = await SeedWorkHour(owner);
            using var context = _fixture.CreateContext();

            await CreateService(context, admin).DeleteWorkHour(wh.WorkHourId);

            using var verify = _fixture.CreateContext();
            Assert.False(await verify.WorkHours.AnyAsync(w => w.WorkHourId == wh.WorkHourId));
        }

        [Fact]
        public async Task Delete_LockedEntry_ThrowsLocked()
        {
            var owner = await SeedUser();
            var admin = await SeedUser(isAdmin: true);
            var wh = await SeedWorkHour(owner, status: 1, approvedBy: admin);
            using var context = _fixture.CreateContext();

            await Assert.ThrowsAsync<EntityLockedException>(() => CreateService(context, owner).DeleteWorkHour(wh.WorkHourId));
        }

        [Fact]
        public async Task Delete_OtherNonAdmin_ThrowsForbidden()
        {
            var owner = await SeedUser();
            var other = await SeedUser();
            var wh = await SeedWorkHour(owner);
            using var context = _fixture.CreateContext();

            await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateService(context, other).DeleteWorkHour(wh.WorkHourId));
        }

        [Fact]
        public async Task Delete_NotFound_ThrowsEntityNotFound()
        {
            var owner = await SeedUser();
            using var context = _fixture.CreateContext();

            await Assert.ThrowsAsync<EntityNotFoundException>(() => CreateService(context, owner).DeleteWorkHour(int.MaxValue));
        }

        #endregion

        #region Read access

        [Fact]
        public async Task GetById_Owner_ReturnsWithNames()
        {
            var owner = await SeedUser();
            var admin = await SeedUser(isAdmin: true, firstName: "Per", lastName: "Sjef");
            var wh = await SeedWorkHour(owner, status: 1, approvedBy: admin);
            using var context = _fixture.CreateContext();

            var result = await CreateService(context, owner).GetWorkHourById(wh.WorkHourId);

            Assert.Equal(wh.WorkHourId, result.WorkHourId);
            Assert.Equal("Per Sjef", result.ApprovedByName);
            Assert.Null(result.ModifiedByName);
            Assert.Equal(DateTimeKind.Utc, result.StartTime!.Value.Kind);
        }

        [Fact]
        public async Task GetById_OtherNonAdmin_ThrowsForbidden()
        {
            var owner = await SeedUser();
            var other = await SeedUser();
            var wh = await SeedWorkHour(owner);
            using var context = _fixture.CreateContext();

            await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateService(context, other).GetWorkHourById(wh.WorkHourId));
        }

        [Fact]
        public async Task GetById_Admin_ReturnsAnyEntry()
        {
            var owner = await SeedUser();
            var admin = await SeedUser(isAdmin: true);
            var wh = await SeedWorkHour(owner);
            using var context = _fixture.CreateContext();

            var result = await CreateService(context, admin).GetWorkHourById(wh.WorkHourId);
            Assert.Equal(owner.UserId, result.UserId);
        }

        [Fact]
        public async Task GetById_NotFound_ThrowsEntityNotFound()
        {
            var owner = await SeedUser();
            using var context = _fixture.CreateContext();

            await Assert.ThrowsAsync<EntityNotFoundException>(() => CreateService(context, owner).GetWorkHourById(int.MaxValue));
        }

        [Fact]
        public async Task GetByUser_Self_ReturnsFilteredAndPaged()
        {
            var owner = await SeedUser();
            var admin = await SeedUser(isAdmin: true);
            await SeedWorkHour(owner);
            await SeedWorkHour(owner);
            await SeedWorkHour(owner, status: 1, approvedBy: admin);
            using var context = _fixture.CreateContext();
            var service = CreateService(context, owner);

            var all = await service.GetWorkHoursByUser(owner.UserId, null);
            var open = await service.GetWorkHoursByUser(owner.UserId, 3);
            var approved = await service.GetWorkHoursByUser(owner.UserId, 1);
            var paged = await service.GetWorkHoursByUser(owner.UserId, null, page: 1, pageSize: 2);

            Assert.Equal(3, all.TotalCount);
            Assert.Equal(2, open.TotalCount);
            Assert.Equal(1, approved.TotalCount);
            Assert.Equal(3, paged.TotalCount);
            Assert.Equal(2, paged.Result.Count());
        }

        [Fact]
        public async Task GetByUser_OtherNonAdmin_ThrowsForbidden()
        {
            var owner = await SeedUser();
            var other = await SeedUser();
            using var context = _fixture.CreateContext();

            await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateService(context, other).GetWorkHoursByUser(owner.UserId, null));
        }

        [Fact]
        public async Task GetWorkHours_NonAdmin_ThrowsForbidden()
        {
            var user = await SeedUser();
            using var context = _fixture.CreateContext();

            await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateService(context, user).GetWorkHours(null));
        }

        [Fact]
        public async Task GetWorkHours_Admin_ReturnsAll()
        {
            var owner = await SeedUser();
            var admin = await SeedUser(isAdmin: true);
            await SeedWorkHour(owner);
            using var context = _fixture.CreateContext();

            var result = await CreateService(context, admin).GetWorkHours(null);
            Assert.True(result.TotalCount >= 1);
        }

        [Fact]
        public async Task GetSum_Self_ReturnsSumsByStatus()
        {
            var owner = await SeedUser();
            var admin = await SeedUser(isAdmin: true);
            await SeedWorkHour(owner);
            await SeedWorkHour(owner, status: 1, approvedBy: admin);
            await SeedWorkHour(owner, status: 2, approvedBy: admin);
            await SeedWorkHour(owner, status: 2, approvedBy: admin);
            using var context = _fixture.CreateContext();

            var result = await CreateService(context, owner).GetWorkHoursSum(owner.UserId);

            Assert.Equal(3.0, result.PendingHours);
            Assert.Equal(3.0, result.ApprovedHours);
            Assert.Equal(6.0, result.RejectedHours);
        }

        [Fact]
        public async Task GetSum_OtherUser_NonAdmin_ThrowsForbidden()
        {
            var owner = await SeedUser();
            var other = await SeedUser();
            using var context = _fixture.CreateContext();

            await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateService(context, other).GetWorkHoursSum(owner.UserId));
        }

        [Fact]
        public async Task GetSum_WithoutUser_NonAdmin_ThrowsForbidden()
        {
            var user = await SeedUser();
            using var context = _fixture.CreateContext();

            await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateService(context, user).GetWorkHoursSum(null));
        }

        [Fact]
        public async Task GetSum_OtherUser_Admin_Allowed()
        {
            var owner = await SeedUser();
            var admin = await SeedUser(isAdmin: true);
            await SeedWorkHour(owner);
            using var context = _fixture.CreateContext();

            var result = await CreateService(context, admin).GetWorkHoursSum(owner.UserId);
            Assert.Equal(3.0, result.PendingHours);
        }

        [Fact]
        public async Task GetSumPerUser_NonAdmin_ThrowsForbidden()
        {
            var user = await SeedUser();
            using var context = _fixture.CreateContext();

            await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateService(context, user).GetWorkHoursSumPerUser());
        }

        #endregion
    }
}
