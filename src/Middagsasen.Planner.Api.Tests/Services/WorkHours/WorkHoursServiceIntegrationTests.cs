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
            => new(new WorkHourRepository(context), MockCurrentUser(user.UserId, user.IsAdmin), Clock);

        /// <summary>Fast «nå» (1. oktober 2026) slik at sesongvalidering og inneværende sesong er deterministisk.</summary>
        private static readonly TimeProvider Clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));

        private async Task<User> SeedUser(bool isAdmin = false, string firstName = "Test", string lastName = "User")
        {
            using var context = _fixture.CreateContext();
            var user = new User
            {
                UserName = $"user_{Guid.NewGuid():N}",
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

        /// <summary>Seeder en føring på 3 timer som starter på <paramref name="startTime"/>.</summary>
        private async Task<WorkHour> SeedWorkHourAt(User owner, DateTime startTime, int? status = null, User? approvedBy = null)
        {
            using var context = _fixture.CreateContext();
            var workHour = new WorkHour
            {
                UserId = owner.UserId,
                StartTime = startTime,
                EndTime = startTime.AddHours(3),
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

            await Assert.ThrowsAsync<DomainValidationException>(() =>
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

            await Assert.ThrowsAsync<DomainValidationException>(() =>
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

            var all = await service.GetWorkHoursByUser(owner.UserId, null, null);
            var open = await service.GetWorkHoursByUser(owner.UserId, 3, null);
            var approved = await service.GetWorkHoursByUser(owner.UserId, 1, null);
            var paged = await service.GetWorkHoursByUser(owner.UserId, null, null, page: 1, pageSize: 2);

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

            await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateService(context, other).GetWorkHoursByUser(owner.UserId, null, null));
        }

        [Fact]
        public async Task GetWorkHours_NonAdmin_ThrowsForbidden()
        {
            var user = await SeedUser();
            using var context = _fixture.CreateContext();

            await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateService(context, user).GetWorkHours(null, null, null));
        }

        [Fact]
        public async Task GetWorkHours_Admin_ReturnsAll()
        {
            var owner = await SeedUser();
            var admin = await SeedUser(isAdmin: true);
            await SeedWorkHour(owner);
            using var context = _fixture.CreateContext();

            var result = await CreateService(context, admin).GetWorkHours(null, null, null);
            Assert.True(result.TotalCount >= 1);
        }

        [Fact]
        public async Task GetWorkHours_FilteredOnSeason_ReturnsOnlyEntriesInSeason()
        {
            var owner = await SeedUser();
            var admin = await SeedUser(isAdmin: true);
            // StartTime lagres i UTC; sesongen starter 1. juli 00:00 norsk tid = 30. juni 22:00 UTC (sommertid).
            var first = await SeedWorkHourAt(owner, new DateTime(2024, 6, 30, 22, 0, 0)); // 1. juli 00:00 Oslo
            var last = await SeedWorkHourAt(owner, new DateTime(2025, 6, 30, 21, 59, 0)); // 30. juni 23:59 Oslo
            await SeedWorkHourAt(owner, new DateTime(2024, 6, 30, 21, 59, 0)); // forrige sesong
            await SeedWorkHourAt(owner, new DateTime(2025, 6, 30, 22, 0, 0));  // neste sesong
            using var context = _fixture.CreateContext();

            var result = await CreateService(context, admin).GetWorkHours(owner.UserId, null, 2024, pageSize: 100);

            Assert.Equal(2, result.TotalCount);
            Assert.Equal(new[] { last.WorkHourId, first.WorkHourId }, result.Result.Select(r => r.WorkHourId));
        }

        [Fact]
        public async Task GetWorkHours_FilteredOnUser_ReturnsOnlyThatUsersEntries()
        {
            var owner = await SeedUser();
            var other = await SeedUser();
            var admin = await SeedUser(isAdmin: true);
            await SeedWorkHour(owner);
            await SeedWorkHour(owner);
            await SeedWorkHour(other);
            using var context = _fixture.CreateContext();

            var result = await CreateService(context, admin).GetWorkHours(owner.UserId, null, null, pageSize: 100);

            Assert.Equal(2, result.TotalCount);
            Assert.All(result.Result, r => Assert.Equal(owner.UserId, r.UserId));
        }

        [Fact]
        public async Task GetByUser_FilteredOnSeason_ReturnsOnlyEntriesInSeason()
        {
            var owner = await SeedUser();
            await SeedWorkHourAt(owner, new DateTime(2024, 9, 1, 9, 0, 0));
            await SeedWorkHourAt(owner, new DateTime(2023, 9, 1, 9, 0, 0));
            using var context = _fixture.CreateContext();

            var result = await CreateService(context, owner).GetWorkHoursByUser(owner.UserId, null, 2023);

            Assert.Equal(1, result.TotalCount);
            Assert.Equal(new DateTime(2023, 9, 1, 9, 0, 0), result.Result.Single().StartTime);
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
        public async Task GetSum_WithSeason_RespectsLowerAndUpperBound()
        {
            var owner = await SeedUser();
            var admin = await SeedUser(isAdmin: true);
            await SeedWorkHourAt(owner, new DateTime(2024, 6, 30, 22, 0, 0));                               // 3 t, åpen (1. juli 00:00 Oslo)
            await SeedWorkHourAt(owner, new DateTime(2025, 6, 30, 21, 0, 0), status: 1, approvedBy: admin); // 3 t, godkjent (30. juni 23:00 Oslo)
            await SeedWorkHourAt(owner, new DateTime(2024, 6, 30, 21, 0, 0));                               // forrige sesong
            await SeedWorkHourAt(owner, new DateTime(2025, 6, 30, 22, 0, 0), status: 1, approvedBy: admin); // neste sesong
            using var context = _fixture.CreateContext();
            var service = CreateService(context, owner);

            var season = await service.GetWorkHoursSum(owner.UserId, 2024);
            var all = await service.GetWorkHoursSum(owner.UserId);

            Assert.Equal(3.0, season.PendingHours);
            Assert.Equal(3.0, season.ApprovedHours);
            Assert.Equal(6.0, all.PendingHours);
            Assert.Equal(6.0, all.ApprovedHours);
        }

        [Fact]
        public async Task GetByUser_SeasonBoundary_IsEvaluatedInOsloTime()
        {
            var owner = await SeedUser();
            var afterMidnightOslo = await SeedWorkHourAt(owner, new DateTime(2025, 6, 30, 22, 30, 0));  // 1. juli 00:30 Oslo
            var beforeMidnightOslo = await SeedWorkHourAt(owner, new DateTime(2025, 6, 30, 21, 30, 0)); // 30. juni 23:30 Oslo
            using var context = _fixture.CreateContext();
            var service = CreateService(context, owner);

            var season2025 = await service.GetWorkHoursByUser(owner.UserId, null, 2025);
            var season2024 = await service.GetWorkHoursByUser(owner.UserId, null, 2024);

            Assert.Equal(new[] { afterMidnightOslo.WorkHourId }, season2025.Result.Select(r => r.WorkHourId));
            Assert.Equal(new[] { beforeMidnightOslo.WorkHourId }, season2024.Result.Select(r => r.WorkHourId));
        }

        [Fact]
        public async Task GetByUser_InvalidSeason_ThrowsInvalidOperation()
        {
            var owner = await SeedUser();
            using var context = _fixture.CreateContext();

            await Assert.ThrowsAsync<DomainValidationException>(() =>
                CreateService(context, owner).GetWorkHoursByUser(owner.UserId, null, 9999));
        }

        [Fact]
        public async Task GetSumPerUser_NonAdmin_ThrowsForbidden()
        {
            var user = await SeedUser();
            using var context = _fixture.CreateContext();

            await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateService(context, user).GetWorkHoursSumPerUser());
        }

        #endregion

        #region Validering av tider

        [Theory]
        [InlineData(-1)]
        [InlineData(0)]
        public async Task Create_EndNotAfterStart_ThrowsInvalidOperation(int endOffsetHours)
        {
            var user = await SeedUser();
            using var context = _fixture.CreateContext();
            var service = CreateService(context, user);

            var ex = await Assert.ThrowsAsync<DomainValidationException>(() =>
                service.CreateWorkHour(new CreateWorkHourRequest { StartTime = Start, EndTime = Start.AddHours(endOffsetHours) }));
            Assert.Equal(WorkHoursService.EndBeforeStartMessage, ex.Message);
        }

        [Fact]
        public async Task Create_WithoutEndTime_IsAllowed()
        {
            var user = await SeedUser();
            using var context = _fixture.CreateContext();

            var result = await CreateService(context, user).CreateWorkHour(new CreateWorkHourRequest { StartTime = Start });

            Assert.Null(result.EndTime);
        }

        [Fact]
        public async Task Update_OnlyEndTime_BeforeStoredStart_ThrowsInvalidOperation()
        {
            var owner = await SeedUser();
            var wh = await SeedWorkHour(owner);
            using var context = _fixture.CreateContext();

            var ex = await Assert.ThrowsAsync<DomainValidationException>(() => CreateService(context, owner)
                .UpdateWorkHour(wh.WorkHourId, new UpdateWorkHourRequest { EndTime = Start.AddMinutes(-30) }));
            Assert.Equal(WorkHoursService.EndBeforeStartMessage, ex.Message);
            Assert.Equal(End, (await Reload(wh.WorkHourId)).EndTime);
        }

        [Fact]
        public async Task Update_OnlyStartTime_AfterStoredEnd_ThrowsInvalidOperation()
        {
            var owner = await SeedUser();
            var wh = await SeedWorkHour(owner);
            using var context = _fixture.CreateContext();

            await Assert.ThrowsAsync<DomainValidationException>(() => CreateService(context, owner)
                .UpdateWorkHour(wh.WorkHourId, new UpdateWorkHourRequest { StartTime = End.AddHours(1) }));
            Assert.Equal(Start, (await Reload(wh.WorkHourId)).StartTime);
        }

        [Fact]
        public async Task Update_BothTimes_EndBeforeStart_ThrowsInvalidOperation()
        {
            var owner = await SeedUser();
            var wh = await SeedWorkHour(owner);
            using var context = _fixture.CreateContext();

            await Assert.ThrowsAsync<DomainValidationException>(() => CreateService(context, owner)
                .UpdateWorkHour(wh.WorkHourId, new UpdateWorkHourRequest { StartTime = End, EndTime = Start }));
            var db = await Reload(wh.WorkHourId);
            Assert.Equal(Start, db.StartTime);
            Assert.Equal(End, db.EndTime);
        }

        [Fact]
        public async Task Update_EndEqualsStoredStart_ThrowsInvalidOperation()
        {
            var owner = await SeedUser();
            var wh = await SeedWorkHour(owner);
            using var context = _fixture.CreateContext();

            await Assert.ThrowsAsync<DomainValidationException>(() => CreateService(context, owner)
                .UpdateWorkHour(wh.WorkHourId, new UpdateWorkHourRequest { EndTime = Start }));
        }

        [Fact]
        public async Task Update_BothTimesValid_MovingPastStoredValues_Succeeds()
        {
            var owner = await SeedUser();
            var wh = await SeedWorkHour(owner);
            using var context = _fixture.CreateContext();

            // Ny start er etter lagret slutt, men gyldig mot ny slutt.
            var result = await CreateService(context, owner).UpdateWorkHour(wh.WorkHourId,
                new UpdateWorkHourRequest { StartTime = End.AddHours(1), EndTime = End.AddHours(2) });

            Assert.Equal(1.0m, result.Hours);
        }

        [Fact]
        public async Task Update_InvalidTimes_ByOtherNonAdmin_ThrowsForbiddenNotValidation()
        {
            var owner = await SeedUser();
            var other = await SeedUser();
            var wh = await SeedWorkHour(owner);
            using var context = _fixture.CreateContext();

            await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateService(context, other)
                .UpdateWorkHour(wh.WorkHourId, new UpdateWorkHourRequest { EndTime = Start.AddHours(-1) }));
        }

        [Fact]
        public async Task Update_InvalidTimes_OnLockedEntry_ThrowsLockedNotValidation()
        {
            var owner = await SeedUser();
            var admin = await SeedUser(isAdmin: true);
            var wh = await SeedWorkHour(owner, status: 1, approvedBy: admin);
            using var context = _fixture.CreateContext();

            await Assert.ThrowsAsync<EntityLockedException>(() => CreateService(context, owner)
                .UpdateWorkHour(wh.WorkHourId, new UpdateWorkHourRequest { EndTime = Start.AddHours(-1) }));
        }

        #endregion

        #region Rekkefølge på sjekker (404 → 403 → 409 → 400)

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(-1)]
        public async Task Update_NonAdminSendsAnyStatus_ThrowsForbidden(int status)
        {
            var owner = await SeedUser();
            var wh = await SeedWorkHour(owner);
            using var context = _fixture.CreateContext();

            await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateService(context, owner)
                .UpdateWorkHour(wh.WorkHourId, new UpdateWorkHourRequest { ApprovalStatus = status }));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        public async Task UpdateApprovedBy_NonAdminSendsAnyStatus_ThrowsForbidden(int status)
        {
            var owner = await SeedUser();
            var wh = await SeedWorkHour(owner);
            using var context = _fixture.CreateContext();

            await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateService(context, owner)
                .UpdateApprovedBy(wh.WorkHourId, new ApprovedByRequest { ApprovalStatus = status }));
        }

        [Fact]
        public async Task UpdateApprovedBy_AdminInvalidStatusOnOpen_ThrowsInvalidOperation()
        {
            var owner = await SeedUser();
            var admin = await SeedUser(isAdmin: true);
            var wh = await SeedWorkHour(owner);
            using var context = _fixture.CreateContext();

            await Assert.ThrowsAsync<DomainValidationException>(() => CreateService(context, admin)
                .UpdateApprovedBy(wh.WorkHourId, new ApprovedByRequest { ApprovalStatus = 3 }));
            Assert.Null((await Reload(wh.WorkHourId)).ApprovalStatus);
        }

        [Fact]
        public async Task Update_AdminInvalidStatusOnLocked_ThrowsLockedBeforeValidation()
        {
            var owner = await SeedUser();
            var admin = await SeedUser(isAdmin: true);
            var wh = await SeedWorkHour(owner, status: 1, approvedBy: admin);
            using var context = _fixture.CreateContext();

            await Assert.ThrowsAsync<EntityLockedException>(() => CreateService(context, admin)
                .UpdateWorkHour(wh.WorkHourId, new UpdateWorkHourRequest { ApprovalStatus = 3 }));
        }

        [Fact]
        public async Task Update_OwnerContentOnLockedPlusStatus_ThrowsForbiddenBeforeLocked()
        {
            var owner = await SeedUser();
            var admin = await SeedUser(isAdmin: true);
            var wh = await SeedWorkHour(owner, status: 1, approvedBy: admin);
            using var context = _fixture.CreateContext();

            await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateService(context, owner)
                .UpdateWorkHour(wh.WorkHourId, new UpdateWorkHourRequest { Description = "x", ApprovalStatus = 1 }));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Update_UnchangedValuesOnLocked_ReturnsEntryWithoutConflict(bool asAdmin)
        {
            var owner = await SeedUser();
            var admin = await SeedUser(isAdmin: true);
            var wh = await SeedWorkHour(owner, status: 1, approvedBy: admin);
            using var context = _fixture.CreateContext();

            var result = await CreateService(context, asAdmin ? admin : owner).UpdateWorkHour(wh.WorkHourId,
                new UpdateWorkHourRequest { StartTime = Start, EndTime = End, Description = "Opprinnelig" });

            Assert.Equal(wh.WorkHourId, result.WorkHourId);
            Assert.Equal(1, result.ApprovalStatus);
            Assert.Null(result.ModifiedBy);
        }

        [Fact]
        public async Task Update_UnchangedValuesOnLocked_ByOtherNonAdmin_ThrowsForbidden()
        {
            var owner = await SeedUser();
            var other = await SeedUser();
            var admin = await SeedUser(isAdmin: true);
            var wh = await SeedWorkHour(owner, status: 1, approvedBy: admin);
            using var context = _fixture.CreateContext();

            await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateService(context, other).UpdateWorkHour(wh.WorkHourId,
                new UpdateWorkHourRequest { StartTime = Start, Description = "Opprinnelig" }));
        }

        #endregion

        #region Samtidighet (ApprovalStatus som concurrency token)

        /// <summary>Repository som kjører <see cref="BeforeSave"/> rett før lagring, for å simulere et race.</summary>
        private sealed class RacingRepository : IWorkHourRepository
        {
            private readonly WorkHourRepository _inner;
            public RacingRepository(PlannerDbContext context) => _inner = new WorkHourRepository(context);
            public Func<Task>? BeforeSave { get; set; }

            public Task<WorkHour?> GetWorkHourById(int workHourId) => _inner.GetWorkHourById(workHourId);
            public Task<WorkHour?> GetWorkHourByIdReadOnly(int workHourId) => _inner.GetWorkHourByIdReadOnly(workHourId);
            public Task<(IReadOnlyList<WorkHour> Items, int TotalCount)> GetWorkHours(int? userId, int? approved, DateTime? from, DateTime? to, int skip, int take)
                => _inner.GetWorkHours(userId, approved, from, to, skip, take);
            public Task<IReadOnlyList<WorkHourInterval>> GetIntervals(int? userId, DateTime? from = null, DateTime? to = null)
                => _inner.GetIntervals(userId, from, to);
            public void Add(WorkHour workHour) => _inner.Add(workHour);
            public void Remove(WorkHour workHour) => _inner.Remove(workHour);

            public async Task SaveChangesAsync()
            {
                if (BeforeSave != null)
                {
                    var action = BeforeSave;
                    BeforeSave = null;
                    await action();
                }
                await _inner.SaveChangesAsync();
            }
        }

        private (WorkHoursService Service, RacingRepository Repository) CreateRacingService(PlannerDbContext context, User user)
        {
            var repository = new RacingRepository(context);
            return (new WorkHoursService(repository, MockCurrentUser(user.UserId, user.IsAdmin), Clock), repository);
        }

        /// <summary>Endrer status direkte i databasen via en annen context, utenom servicen.</summary>
        private Func<Task> SetStatusInDatabase(int workHourId, int? status, User? approvedBy) => async () =>
        {
            using var other = _fixture.CreateContext();
            var approvedById = approvedBy?.UserId;
            await other.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE WorkHours SET ApprovalStatus = {status}, ApprovedBy = {approvedById} WHERE WorkHourId = {workHourId}");
        };

        [Fact]
        public async Task Race_OwnerEditsWhileAdminApproves_ThrowsLockedAndContentUnchanged()
        {
            var owner = await SeedUser();
            var admin = await SeedUser(isAdmin: true);
            var wh = await SeedWorkHour(owner);
            using var context = _fixture.CreateContext();
            var (service, repository) = CreateRacingService(context, owner);
            repository.BeforeSave = SetStatusInDatabase(wh.WorkHourId, 1, admin);

            var ex = await Assert.ThrowsAsync<EntityLockedException>(() => service.UpdateWorkHour(wh.WorkHourId,
                new UpdateWorkHourRequest { Description = "Etter race", EndTime = End.AddHours(1) }));

            Assert.Equal(WorkHoursService.LockedMessage, ex.Message);
            var db = await Reload(wh.WorkHourId);
            Assert.Equal("Opprinnelig", db.Description);
            Assert.Equal(End, db.EndTime);
            Assert.Equal(1, db.ApprovalStatus);
            Assert.Equal(admin.UserId, db.ApprovedBy);
        }

        [Fact]
        public async Task Race_AdminEditsAndApprovesWhileOtherAdminRejects_ThrowsLocked()
        {
            var owner = await SeedUser();
            var admin = await SeedUser(isAdmin: true);
            var otherAdmin = await SeedUser(isAdmin: true);
            var wh = await SeedWorkHour(owner);
            using var context = _fixture.CreateContext();
            var (service, repository) = CreateRacingService(context, admin);
            repository.BeforeSave = SetStatusInDatabase(wh.WorkHourId, 2, otherAdmin);

            await Assert.ThrowsAsync<EntityLockedException>(() => service.UpdateWorkHour(wh.WorkHourId,
                new UpdateWorkHourRequest { Description = "Rettet", ApprovalStatus = 1 }));

            var db = await Reload(wh.WorkHourId);
            Assert.Equal("Opprinnelig", db.Description);
            Assert.Equal(2, db.ApprovalStatus);
            Assert.Equal(otherAdmin.UserId, db.ApprovedBy);
            Assert.Null(db.ModifiedBy);
        }

        [Fact]
        public async Task Race_UpdateApprovedBy_ApproveWhileOtherAdminRejects_ThrowsLocked()
        {
            var owner = await SeedUser();
            var admin = await SeedUser(isAdmin: true);
            var otherAdmin = await SeedUser(isAdmin: true);
            var wh = await SeedWorkHour(owner);
            using var context = _fixture.CreateContext();
            var (service, repository) = CreateRacingService(context, admin);
            repository.BeforeSave = SetStatusInDatabase(wh.WorkHourId, 2, otherAdmin);

            await Assert.ThrowsAsync<EntityLockedException>(() =>
                service.UpdateApprovedBy(wh.WorkHourId, new ApprovedByRequest { ApprovalStatus = 1 }));

            var db = await Reload(wh.WorkHourId);
            Assert.Equal(2, db.ApprovalStatus);
            Assert.Equal(otherAdmin.UserId, db.ApprovedBy);
        }

        [Fact]
        public async Task Race_UpdateApprovedBy_ResetWhileOtherAdminAlreadyReset_ThrowsLocked()
        {
            var owner = await SeedUser();
            var admin = await SeedUser(isAdmin: true);
            var wh = await SeedWorkHour(owner, status: 1, approvedBy: admin);
            using var context = _fixture.CreateContext();
            var (service, repository) = CreateRacingService(context, admin);
            repository.BeforeSave = SetStatusInDatabase(wh.WorkHourId, null, null);

            await Assert.ThrowsAsync<EntityLockedException>(() =>
                service.UpdateApprovedBy(wh.WorkHourId, new ApprovedByRequest { ApprovalStatus = null }));
        }

        [Fact]
        public async Task Race_DeleteWhileAdminApproves_ThrowsLockedAndEntryKept()
        {
            var owner = await SeedUser();
            var admin = await SeedUser(isAdmin: true);
            var wh = await SeedWorkHour(owner);
            using var context = _fixture.CreateContext();
            var (service, repository) = CreateRacingService(context, owner);
            repository.BeforeSave = SetStatusInDatabase(wh.WorkHourId, 1, admin);

            await Assert.ThrowsAsync<EntityLockedException>(() => service.DeleteWorkHour(wh.WorkHourId));

            Assert.Equal(1, (await Reload(wh.WorkHourId)).ApprovalStatus);
        }

        [Fact]
        public async Task Update_OpenEntry_GeneratesWhereApprovalStatusIsNull()
        {
            var owner = await SeedUser();
            var wh = await SeedWorkHour(owner);
            var sql = new List<string>();
            var options = new DbContextOptionsBuilder<PlannerDbContext>()
                .UseSqlServer(_fixture.ConnectionString)
                .LogTo(sql.Add, new[] { DbLoggerCategory.Database.Command.Name })
                .Options;
            using var context = new PlannerDbContext(options);

            await CreateService(context, owner).UpdateWorkHour(wh.WorkHourId, new UpdateWorkHourRequest { Description = "Ny" });

            // Loggen inneholder både «Executing» og «Executed» for samme kommando.
            var updates = sql.Where(s => s.Contains("UPDATE [WorkHours]")).ToList();
            Assert.NotEmpty(updates);
            Assert.All(updates, u => Assert.Contains("[ApprovalStatus] IS NULL", u));
            Assert.Equal("Ny", (await Reload(wh.WorkHourId)).Description);
        }

        [Fact]
        public async Task NormalFlow_ApproveResetEditReapprove_WorksWithConcurrencyToken()
        {
            var owner = await SeedUser();
            var admin = await SeedUser(isAdmin: true);
            var wh = await SeedWorkHour(owner);

            using (var c = _fixture.CreateContext())
                await CreateService(c, admin).UpdateApprovedBy(wh.WorkHourId, new ApprovedByRequest { ApprovalStatus = 1 });

            // «Ingen status» fra godkjent → null: WHERE ApprovalStatus = 1.
            using (var c = _fixture.CreateContext())
                await CreateService(c, admin).UpdateApprovedBy(wh.WorkHourId, new ApprovedByRequest { ApprovalStatus = null });
            Assert.Null((await Reload(wh.WorkHourId)).ApprovalStatus);

            // Redigering av nå åpen føring: WHERE ApprovalStatus IS NULL.
            using (var c = _fixture.CreateContext())
                await CreateService(c, owner).UpdateWorkHour(wh.WorkHourId, new UpdateWorkHourRequest { Description = "Rettet" });

            using (var c = _fixture.CreateContext())
                await CreateService(c, admin).UpdateWorkHour(wh.WorkHourId, new UpdateWorkHourRequest { ApprovalStatus = 2 });

            var db = await Reload(wh.WorkHourId);
            Assert.Equal("Rettet", db.Description);
            Assert.Equal(2, db.ApprovalStatus);
            Assert.Equal(admin.UserId, db.ApprovedBy);
        }

        #endregion
    }
}
