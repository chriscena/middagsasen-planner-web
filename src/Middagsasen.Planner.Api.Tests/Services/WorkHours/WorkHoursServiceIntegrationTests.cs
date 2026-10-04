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

        private async Task<WorkHour> SeedWorkHour(User owner, ApprovalStatus? status = null, User? approvedBy = null, string? description = "Opprinnelig")
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
        private async Task<WorkHour> SeedWorkHourAt(User owner, DateTime startTime, ApprovalStatus? status = null, User? approvedBy = null)
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

            var result = await service.UpdateWorkHour(wh.WorkHourId, new UpdateWorkHourRequest { ApprovalStatus = ApprovalStatus.Approved });

            Assert.Equal(ApprovalStatus.Approved, result.ApprovalStatus);
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
                ApprovalStatus = ApprovalStatus.Approved,
            });

            Assert.Equal("Rettet", result.Description);
            Assert.Equal(ApprovalStatus.Approved, result.ApprovalStatus);
            Assert.Equal(admin.UserId, result.ApprovedBy);
            Assert.Equal("Gunn Godkjenner", result.ApprovedByName);
            Assert.Equal(admin.UserId, result.ModifiedBy);
            Assert.True(result.ApprovedTime > before);

            var db = await Reload(wh.WorkHourId);
            Assert.Equal("Rettet", db.Description);
            Assert.Equal(ApprovalStatus.Approved, db.ApprovalStatus);
            Assert.Equal(admin.UserId, db.ApprovedBy);
            Assert.NotNull(db.ApprovedTime);
        }

        [Fact]
        public async Task Update_ResetStatusToNullViaPatch_IsNotPossible_NullMeansAbsent()
        {
            var owner = await SeedUser();
            var admin = await SeedUser(isAdmin: true);
            var wh = await SeedWorkHour(owner, status: ApprovalStatus.Approved, approvedBy: admin);
            using var context = _fixture.CreateContext();
            var service = CreateService(context, admin);

            // Tom PATCH (alle felter null) endrer ingenting.
            var result = await service.UpdateWorkHour(wh.WorkHourId, new UpdateWorkHourRequest());

            Assert.Equal(ApprovalStatus.Approved, result.ApprovalStatus);
            Assert.Equal(ApprovalStatus.Approved, (await Reload(wh.WorkHourId)).ApprovalStatus);
        }

        // System.Text.Json godtar ethvert heltall for en enum, så udefinerte verdier må gi 400.
        [Theory]
        [InlineData(0)]
        [InlineData(3)]
        [InlineData(-1)]
        public async Task Update_UndefinedStatus_ThrowsValidation(int status)
        {
            var owner = await SeedUser();
            var admin = await SeedUser(isAdmin: true);
            var wh = await SeedWorkHour(owner);
            using var context = _fixture.CreateContext();
            var service = CreateService(context, admin);

            var ex = await Assert.ThrowsAsync<DomainValidationException>(() =>
                service.UpdateWorkHour(wh.WorkHourId, new UpdateWorkHourRequest { ApprovalStatus = (ApprovalStatus)status }));
            Assert.Equal(WorkHoursService.InvalidStatusMessage, ex.Message);
            Assert.Null((await Reload(wh.WorkHourId)).ApprovalStatus);
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
            var wh = await SeedWorkHour(owner, status: ApprovalStatus.Approved, approvedBy: admin);
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
                service.UpdateWorkHour(wh.WorkHourId, new UpdateWorkHourRequest { Description = "x", ApprovalStatus = ApprovalStatus.Approved }));
            var db = await Reload(wh.WorkHourId);
            Assert.Equal("Opprinnelig", db.Description);
            Assert.Null(db.ApprovalStatus);
        }

        [Theory]
        [InlineData(false, ApprovalStatus.Approved)]
        [InlineData(false, ApprovalStatus.Rejected)]
        [InlineData(true, ApprovalStatus.Approved)]
        [InlineData(true, ApprovalStatus.Rejected)]
        public async Task Update_LockedEntry_OwnerOrAdmin_ThrowsLocked(bool asAdmin, ApprovalStatus status)
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
            var wh = await SeedWorkHour(owner, status: ApprovalStatus.Rejected, approvedBy: admin);
            using var context = _fixture.CreateContext();
            var service = CreateService(context, admin);

            await Assert.ThrowsAsync<EntityLockedException>(() =>
                service.UpdateWorkHour(wh.WorkHourId, new UpdateWorkHourRequest { ApprovalStatus = ApprovalStatus.Approved }));
            Assert.Equal(ApprovalStatus.Rejected, (await Reload(wh.WorkHourId)).ApprovalStatus);
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
            var result = await service.UpdateApprovedBy(wh.WorkHourId, new ApprovedByRequest { ApprovalStatus = ApprovalStatus.Rejected });

            Assert.Equal(ApprovalStatus.Rejected, result.ApprovalStatus);
            Assert.Equal(admin.UserId, result.ApprovedBy);
            Assert.True(result.ApprovedTime > before);
            var db = await Reload(wh.WorkHourId);
            Assert.Equal(ApprovalStatus.Rejected, db.ApprovalStatus);
            Assert.Equal(admin.UserId, db.ApprovedBy);
            Assert.Null(db.ModifiedBy);
        }

        [Fact]
        public async Task UpdateApprovedBy_ResetToNull_ClearsApprovedByAndTime()
        {
            var owner = await SeedUser();
            var admin = await SeedUser(isAdmin: true);
            var wh = await SeedWorkHour(owner, status: ApprovalStatus.Approved, approvedBy: admin);
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
                service.UpdateApprovedBy(wh.WorkHourId, new ApprovedByRequest { ApprovalStatus = ApprovalStatus.Approved }));
            Assert.Null((await Reload(wh.WorkHourId)).ApprovalStatus);
        }

        [Fact]
        public async Task UpdateApprovedBy_NotFound_ThrowsEntityNotFound()
        {
            var admin = await SeedUser(isAdmin: true);
            using var context = _fixture.CreateContext();
            var service = CreateService(context, admin);

            await Assert.ThrowsAsync<EntityNotFoundException>(() =>
                service.UpdateApprovedBy(int.MaxValue, new ApprovedByRequest { ApprovalStatus = ApprovalStatus.Approved }));
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
            var wh = await SeedWorkHour(owner, status: ApprovalStatus.Approved, approvedBy: admin);
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
            var wh = await SeedWorkHour(owner, status: ApprovalStatus.Approved, approvedBy: admin);
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
            await SeedWorkHour(owner, status: ApprovalStatus.Approved, approvedBy: admin);
            using var context = _fixture.CreateContext();
            var service = CreateService(context, owner);

            var all = await service.GetWorkHoursByUser(owner.UserId, null, null);
            var open = await service.GetWorkHoursByUser(owner.UserId, ApprovalFilter.Pending, null);
            var approved = await service.GetWorkHoursByUser(owner.UserId, ApprovalFilter.Approved, null);
            var paged = await service.GetWorkHoursByUser(owner.UserId, null, null, page: 1, pageSize: 2);

            Assert.Equal(3, all.TotalCount);
            Assert.Equal(2, open.TotalCount);
            Assert.Equal(1, approved.TotalCount);
            Assert.Equal(3, paged.TotalCount);
            Assert.Equal(2, paged.Result.Count());
        }

        [Theory]
        [InlineData(ApprovalFilter.All, 4)]
        [InlineData(ApprovalFilter.Pending, 2)]
        [InlineData(ApprovalFilter.Approved, 1)]
        [InlineData(ApprovalFilter.Rejected, 1)]
        public async Task GetWorkHours_FilteredOnStatus_ReturnsMatchingEntries(ApprovalFilter filter, int expected)
        {
            var owner = await SeedUser();
            var admin = await SeedUser(isAdmin: true);
            await SeedWorkHour(owner);
            await SeedWorkHour(owner);
            await SeedWorkHour(owner, status: ApprovalStatus.Approved, approvedBy: admin);
            await SeedWorkHour(owner, status: ApprovalStatus.Rejected, approvedBy: admin);
            using var context = _fixture.CreateContext();

            var result = await CreateService(context, admin).GetWorkHours(owner.UserId, filter, null, pageSize: 100);

            Assert.Equal(expected, result.TotalCount);
            ApprovalStatus? expectedStatus = filter switch
            {
                ApprovalFilter.Approved => ApprovalStatus.Approved,
                ApprovalFilter.Rejected => ApprovalStatus.Rejected,
                _ => null,
            };
            if (filter != ApprovalFilter.All)
                Assert.All(result.Result, r => Assert.Equal(expectedStatus, r.ApprovalStatus));
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
            await SeedWorkHour(owner, status: ApprovalStatus.Approved, approvedBy: admin);
            await SeedWorkHour(owner, status: ApprovalStatus.Rejected, approvedBy: admin);
            await SeedWorkHour(owner, status: ApprovalStatus.Rejected, approvedBy: admin);
            using var context = _fixture.CreateContext();

            var result = await CreateService(context, owner).GetWorkHoursSum(owner.UserId);

            Assert.Equal(3.0, result.PendingHours);
            Assert.Equal(3.0, result.ApprovedHours);
            Assert.Equal(6.0, result.RejectedHours);
        }

        [Fact]
        public async Task GetSum_UndefinedStatusInDatabase_CountsAsPending()
        {
            // Eldre rader kan ha uvaliderte verdier (f.eks. 0) siden kolonnen mangler CHECK-constraint.
            var owner = await SeedUser();
            var admin = await SeedUser(isAdmin: true);
            var legacy = await SeedWorkHour(owner);
            await SeedWorkHour(owner);
            await SeedWorkHour(owner, status: ApprovalStatus.Approved, approvedBy: admin);
            await SetStatusInDatabase(legacy.WorkHourId, (ApprovalStatus)0, null)();
            using var context = _fixture.CreateContext();

            var result = await CreateService(context, owner).GetWorkHoursSum(owner.UserId);

            Assert.Equal(6.0, result.PendingHours);
            Assert.Equal(3.0, result.ApprovedHours);
            Assert.Equal(0.0, result.RejectedHours);
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
            await SeedWorkHourAt(owner, new DateTime(2025, 6, 30, 21, 0, 0), status: ApprovalStatus.Approved, approvedBy: admin); // 3 t, godkjent (30. juni 23:00 Oslo)
            await SeedWorkHourAt(owner, new DateTime(2024, 6, 30, 21, 0, 0));                               // forrige sesong
            await SeedWorkHourAt(owner, new DateTime(2025, 6, 30, 22, 0, 0), status: ApprovalStatus.Approved, approvedBy: admin); // neste sesong
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
            var wh = await SeedWorkHour(owner, status: ApprovalStatus.Approved, approvedBy: admin);
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
                .UpdateWorkHour(wh.WorkHourId, new UpdateWorkHourRequest { ApprovalStatus = (ApprovalStatus)status }));
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
                .UpdateApprovedBy(wh.WorkHourId, new ApprovedByRequest { ApprovalStatus = (ApprovalStatus)status }));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(3)]
        public async Task UpdateApprovedBy_AdminUndefinedStatusOnOpen_ThrowsValidation(int status)
        {
            var owner = await SeedUser();
            var admin = await SeedUser(isAdmin: true);
            var wh = await SeedWorkHour(owner);
            using var context = _fixture.CreateContext();

            await Assert.ThrowsAsync<DomainValidationException>(() => CreateService(context, admin)
                .UpdateApprovedBy(wh.WorkHourId, new ApprovedByRequest { ApprovalStatus = (ApprovalStatus)status }));
            Assert.Null((await Reload(wh.WorkHourId)).ApprovalStatus);
        }

        [Fact]
        public async Task Update_AdminInvalidStatusOnLocked_ThrowsLockedBeforeValidation()
        {
            var owner = await SeedUser();
            var admin = await SeedUser(isAdmin: true);
            var wh = await SeedWorkHour(owner, status: ApprovalStatus.Approved, approvedBy: admin);
            using var context = _fixture.CreateContext();

            await Assert.ThrowsAsync<EntityLockedException>(() => CreateService(context, admin)
                .UpdateWorkHour(wh.WorkHourId, new UpdateWorkHourRequest { ApprovalStatus = (ApprovalStatus)3 }));
        }

        [Fact]
        public async Task Update_OwnerContentOnLockedPlusStatus_ThrowsForbiddenBeforeLocked()
        {
            var owner = await SeedUser();
            var admin = await SeedUser(isAdmin: true);
            var wh = await SeedWorkHour(owner, status: ApprovalStatus.Approved, approvedBy: admin);
            using var context = _fixture.CreateContext();

            await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateService(context, owner)
                .UpdateWorkHour(wh.WorkHourId, new UpdateWorkHourRequest { Description = "x", ApprovalStatus = ApprovalStatus.Approved }));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Update_UnchangedValuesOnLocked_ReturnsEntryWithoutConflict(bool asAdmin)
        {
            var owner = await SeedUser();
            var admin = await SeedUser(isAdmin: true);
            var wh = await SeedWorkHour(owner, status: ApprovalStatus.Approved, approvedBy: admin);
            using var context = _fixture.CreateContext();

            var result = await CreateService(context, asAdmin ? admin : owner).UpdateWorkHour(wh.WorkHourId,
                new UpdateWorkHourRequest { StartTime = Start, EndTime = End, Description = "Opprinnelig" });

            Assert.Equal(wh.WorkHourId, result.WorkHourId);
            Assert.Equal(ApprovalStatus.Approved, result.ApprovalStatus);
            Assert.Null(result.ModifiedBy);
        }

        [Fact]
        public async Task Update_UnchangedValuesOnLocked_ByOtherNonAdmin_ThrowsForbidden()
        {
            var owner = await SeedUser();
            var other = await SeedUser();
            var admin = await SeedUser(isAdmin: true);
            var wh = await SeedWorkHour(owner, status: ApprovalStatus.Approved, approvedBy: admin);
            using var context = _fixture.CreateContext();

            await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateService(context, other).UpdateWorkHour(wh.WorkHourId,
                new UpdateWorkHourRequest { StartTime = Start, Description = "Opprinnelig" }));
        }

        #endregion

        #region Flagg i svaret (canEdit, canDelete, canApprove, canResetStatus)

        public enum Viewer { Owner, Admin }

        /// <summary>Forventede flagg: eier og admin (ikke eier) på åpen, godkjent og avslått føring.</summary>
        public static TheoryData<Viewer, ApprovalStatus?, bool, bool, bool, bool> FlagCases => new()
        {
            // Viewer, status, canEdit, canDelete, canApprove, canResetStatus
            { Viewer.Owner, null, true, true, false, false },
            { Viewer.Owner, ApprovalStatus.Approved, false, false, false, false },
            { Viewer.Owner, ApprovalStatus.Rejected, false, false, false, false },
            { Viewer.Admin, null, true, true, true, false },
            { Viewer.Admin, ApprovalStatus.Approved, false, false, false, true },
            { Viewer.Admin, ApprovalStatus.Rejected, false, false, false, true },
        };

        private static void AssertFlags(WorkHourResponse response, bool canEdit, bool canDelete, bool canApprove, bool canResetStatus)
        {
            Assert.Equal(canEdit, response.CanEdit);
            Assert.Equal(canDelete, response.CanDelete);
            Assert.Equal(canApprove, response.CanApprove);
            Assert.Equal(canResetStatus, response.CanResetStatus);
        }

        /// <summary>Flaggene skal være nøyaktig det policyen sier for aktøren.</summary>
        private static void AssertMatchesPolicy(WorkHourResponse response, WorkHour entry, User viewer)
        {
            var expected = WorkHourPolicy.GetPermissions(entry, new Actor(viewer.UserId, viewer.IsAdmin));
            AssertFlags(response, expected.CanEdit, expected.CanDelete, expected.CanApprove, expected.CanResetStatus);
        }

        [Theory]
        [MemberData(nameof(FlagCases))]
        public async Task Flags_GetById_MatchPolicy(Viewer viewerKind, ApprovalStatus? status,
            bool canEdit, bool canDelete, bool canApprove, bool canResetStatus)
        {
            var owner = await SeedUser();
            var admin = await SeedUser(isAdmin: true);
            var wh = await SeedWorkHour(owner, status: status, approvedBy: status.HasValue ? admin : null);
            var viewer = viewerKind == Viewer.Owner ? owner : admin;
            using var context = _fixture.CreateContext();

            var result = await CreateService(context, viewer).GetWorkHourById(wh.WorkHourId);

            AssertFlags(result, canEdit, canDelete, canApprove, canResetStatus);
            AssertMatchesPolicy(result, wh, viewer);
        }

        [Theory]
        [MemberData(nameof(FlagCases))]
        public async Task Flags_GetWorkHoursByUser_MatchPolicy(Viewer viewerKind, ApprovalStatus? status,
            bool canEdit, bool canDelete, bool canApprove, bool canResetStatus)
        {
            var owner = await SeedUser();
            var admin = await SeedUser(isAdmin: true);
            var wh = await SeedWorkHour(owner, status: status, approvedBy: status.HasValue ? admin : null);
            var viewer = viewerKind == Viewer.Owner ? owner : admin;
            using var context = _fixture.CreateContext();

            var result = await CreateService(context, viewer).GetWorkHoursByUser(owner.UserId, null, null);

            var item = Assert.Single(result.Result);
            AssertFlags(item, canEdit, canDelete, canApprove, canResetStatus);
            AssertMatchesPolicy(item, wh, viewer);
        }

        [Fact]
        public async Task Flags_GetWorkHours_AdminList_PerEntryMatchesPolicy()
        {
            var owner = await SeedUser();
            var admin = await SeedUser(isAdmin: true);
            var open = await SeedWorkHourAt(owner, Start);
            var approved = await SeedWorkHourAt(owner, Start.AddDays(1), status: ApprovalStatus.Approved, approvedBy: admin);
            var rejected = await SeedWorkHourAt(owner, Start.AddDays(2), status: ApprovalStatus.Rejected, approvedBy: admin);
            using var context = _fixture.CreateContext();

            var result = await CreateService(context, admin).GetWorkHours(owner.UserId, null, null, pageSize: 100);

            var byId = result.Result.ToDictionary(r => r.WorkHourId);
            Assert.Equal(3, byId.Count);
            AssertFlags(byId[open.WorkHourId], canEdit: true, canDelete: true, canApprove: true, canResetStatus: false);
            AssertFlags(byId[approved.WorkHourId], canEdit: false, canDelete: false, canApprove: false, canResetStatus: true);
            AssertFlags(byId[rejected.WorkHourId], canEdit: false, canDelete: false, canApprove: false, canResetStatus: true);
            AssertMatchesPolicy(byId[open.WorkHourId], open, admin);
            AssertMatchesPolicy(byId[approved.WorkHourId], approved, admin);
            AssertMatchesPolicy(byId[rejected.WorkHourId], rejected, admin);
        }

        [Fact]
        public async Task Flags_Create_OwnerCanEditAndDelete()
        {
            var owner = await SeedUser();
            using var context = _fixture.CreateContext();

            var result = await CreateService(context, owner).CreateWorkHour(new CreateWorkHourRequest { StartTime = Start, EndTime = End });

            AssertFlags(result, canEdit: true, canDelete: true, canApprove: false, canResetStatus: false);
        }

        [Fact]
        public async Task Flags_Update_ReflectStateAfterApproval()
        {
            var owner = await SeedUser();
            var admin = await SeedUser(isAdmin: true);
            var wh = await SeedWorkHour(owner);
            using var context = _fixture.CreateContext();

            var result = await CreateService(context, admin).UpdateWorkHour(wh.WorkHourId,
                new UpdateWorkHourRequest { ApprovalStatus = ApprovalStatus.Approved });

            AssertFlags(result, canEdit: false, canDelete: false, canApprove: false, canResetStatus: true);
        }

        [Fact]
        public async Task Flags_Delete_AllFalseSinceEntryNoLongerExists()
        {
            var owner = await SeedUser();
            var wh = await SeedWorkHour(owner);
            using var context = _fixture.CreateContext();

            var result = await CreateService(context, owner).DeleteWorkHour(wh.WorkHourId);

            AssertFlags(result, canEdit: false, canDelete: false, canApprove: false, canResetStatus: false);
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
            public Task<(IReadOnlyList<WorkHour> Items, int TotalCount)> GetWorkHours(int? userId, ApprovalFilter approved, DateTime? from, DateTime? to, int skip, int take)
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
        private Func<Task> SetStatusInDatabase(int workHourId, ApprovalStatus? status, User? approvedBy) => async () =>
        {
            using var other = _fixture.CreateContext();
            var approvedById = approvedBy?.UserId;
            var statusValue = (int?)status;
            await other.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE WorkHours SET ApprovalStatus = {statusValue}, ApprovedBy = {approvedById} WHERE WorkHourId = {workHourId}");
        };

        [Fact]
        public async Task Race_OwnerEditsWhileAdminApproves_ThrowsLockedAndContentUnchanged()
        {
            var owner = await SeedUser();
            var admin = await SeedUser(isAdmin: true);
            var wh = await SeedWorkHour(owner);
            using var context = _fixture.CreateContext();
            var (service, repository) = CreateRacingService(context, owner);
            repository.BeforeSave = SetStatusInDatabase(wh.WorkHourId, ApprovalStatus.Approved, admin);

            var ex = await Assert.ThrowsAsync<EntityLockedException>(() => service.UpdateWorkHour(wh.WorkHourId,
                new UpdateWorkHourRequest { Description = "Etter race", EndTime = End.AddHours(1) }));

            Assert.Equal(WorkHoursService.LockedMessage, ex.Message);
            var db = await Reload(wh.WorkHourId);
            Assert.Equal("Opprinnelig", db.Description);
            Assert.Equal(End, db.EndTime);
            Assert.Equal(ApprovalStatus.Approved, db.ApprovalStatus);
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
            repository.BeforeSave = SetStatusInDatabase(wh.WorkHourId, ApprovalStatus.Rejected, otherAdmin);

            await Assert.ThrowsAsync<EntityLockedException>(() => service.UpdateWorkHour(wh.WorkHourId,
                new UpdateWorkHourRequest { Description = "Rettet", ApprovalStatus = ApprovalStatus.Approved }));

            var db = await Reload(wh.WorkHourId);
            Assert.Equal("Opprinnelig", db.Description);
            Assert.Equal(ApprovalStatus.Rejected, db.ApprovalStatus);
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
            repository.BeforeSave = SetStatusInDatabase(wh.WorkHourId, ApprovalStatus.Rejected, otherAdmin);

            await Assert.ThrowsAsync<EntityLockedException>(() =>
                service.UpdateApprovedBy(wh.WorkHourId, new ApprovedByRequest { ApprovalStatus = ApprovalStatus.Approved }));

            var db = await Reload(wh.WorkHourId);
            Assert.Equal(ApprovalStatus.Rejected, db.ApprovalStatus);
            Assert.Equal(otherAdmin.UserId, db.ApprovedBy);
        }

        [Fact]
        public async Task Race_UpdateApprovedBy_ResetWhileOtherAdminAlreadyReset_ThrowsLocked()
        {
            var owner = await SeedUser();
            var admin = await SeedUser(isAdmin: true);
            var wh = await SeedWorkHour(owner, status: ApprovalStatus.Approved, approvedBy: admin);
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
            repository.BeforeSave = SetStatusInDatabase(wh.WorkHourId, ApprovalStatus.Approved, admin);

            await Assert.ThrowsAsync<EntityLockedException>(() => service.DeleteWorkHour(wh.WorkHourId));

            Assert.Equal(ApprovalStatus.Approved, (await Reload(wh.WorkHourId)).ApprovalStatus);
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
                await CreateService(c, admin).UpdateApprovedBy(wh.WorkHourId, new ApprovedByRequest { ApprovalStatus = ApprovalStatus.Approved });

            // «Ingen status» fra godkjent → null: WHERE ApprovalStatus = ApprovalStatus.Approved.
            using (var c = _fixture.CreateContext())
                await CreateService(c, admin).UpdateApprovedBy(wh.WorkHourId, new ApprovedByRequest { ApprovalStatus = null });
            Assert.Null((await Reload(wh.WorkHourId)).ApprovalStatus);

            // Redigering av nå åpen føring: WHERE ApprovalStatus IS NULL.
            using (var c = _fixture.CreateContext())
                await CreateService(c, owner).UpdateWorkHour(wh.WorkHourId, new UpdateWorkHourRequest { Description = "Rettet" });

            using (var c = _fixture.CreateContext())
                await CreateService(c, admin).UpdateWorkHour(wh.WorkHourId, new UpdateWorkHourRequest { ApprovalStatus = ApprovalStatus.Rejected });

            var db = await Reload(wh.WorkHourId);
            Assert.Equal("Rettet", db.Description);
            Assert.Equal(ApprovalStatus.Rejected, db.ApprovalStatus);
            Assert.Equal(admin.UserId, db.ApprovedBy);
        }

        #endregion
    }
}
