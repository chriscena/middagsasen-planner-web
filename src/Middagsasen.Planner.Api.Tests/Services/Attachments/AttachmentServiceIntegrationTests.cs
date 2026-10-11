using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services;
using Middagsasen.Planner.Api.Services.Attachments;
using Middagsasen.Planner.Api.Services.Resources;
using Middagsasen.Planner.Api.Services.Storage;
using Middagsasen.Planner.Api.Tests.Infrastructure;
using NSubstitute;

namespace Middagsasen.Planner.Api.Tests.Services.Attachments
{
    /// <summary>
    /// Integrasjonstester mot <see cref="IAttachmentService"/> med ekte repository, ekte database og falsk
    /// <see cref="IStorageService"/>. «Nå» er 11.10.2026 12:00Z, så inneværende sesong starter 1. juli 2026 norsk tid.
    /// </summary>
    [Collection("Database")]
    public class AttachmentServiceIntegrationTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);
        private static readonly DateTime Earlier = new(2026, 9, 1, 8, 0, 0);

        private readonly DatabaseFixture _fixture;
        private readonly IStorageService _storage = Substitute.For<IStorageService>();

        public AttachmentServiceIntegrationTests(DatabaseFixture fixture)
        {
            _fixture = fixture;
            _storage.Read(Arg.Any<string>()).Returns(Encoding.UTF8.GetBytes("%PDF"));
        }

        private AttachmentService CreateService(PlannerDbContext context, int userId, bool isAdmin = false, DateTimeOffset? now = null)
        {
            var currentUser = Substitute.For<ICurrentUserService>();
            currentUser.UserId.Returns(userId);
            currentUser.IsAdmin.Returns(isAdmin);
            return new AttachmentService(new AttachmentRepository(context), _storage, currentUser, new FakeTimeProvider(now ?? Now));
        }

        private static string UniqueName(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

        private static async Task<User> SeedUser(PlannerDbContext context, string firstName = "Test", string lastName = "Bruker", bool isAdmin = false, bool inactive = false)
        {
            var user = new User
            {
                UserName = UniqueName("user"),
                FirstName = firstName,
                LastName = lastName,
                Created = DateTime.UtcNow,
                IsAdmin = isAdmin,
                Inactive = inactive,
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();
            return user;
        }

        private static async Task<ResourceType> SeedResourceType(PlannerDbContext context, string? name = null)
        {
            var resourceType = new ResourceType { Name = name ?? UniqueName("Heis"), DefaultShiftCount = 2 };
            context.ResourceTypes.Add(resourceType);
            await context.SaveChangesAsync();
            return resourceType;
        }

        private static async Task<ResourceTypeFile> SeedFile(PlannerDbContext context, int resourceTypeId, int createdBy, string description = "Instruks",
            bool requiresAcknowledgement = true, bool isMandatory = false, DateTime? archived = null)
        {
            var file = new ResourceTypeFile
            {
                ResourceTypeId = resourceTypeId,
                StorageName = Guid.NewGuid().ToString(),
                FileName = "instruks.pdf",
                Description = description,
                MimeType = "application/pdf",
                Created = Earlier,
                CreatedBy = createdBy,
                Updated = Earlier,
                UpdatedBy = createdBy,
                RequiresAcknowledgement = requiresAcknowledgement,
                IsMandatory = isMandatory,
                Archived = archived,
                ArchivedBy = archived is null ? null : createdBy,
            };
            context.ResourceTypeFiles.Add(file);
            await context.SaveChangesAsync();
            return file;
        }

        private static async Task<ResourceTypeFileAcknowledgement> SeedAcknowledgement(PlannerDbContext context, int fileId, int userId, string? comment = null)
        {
            var acknowledgement = new ResourceTypeFileAcknowledgement
            {
                ResourceTypeFileId = fileId,
                UserId = userId,
                Acknowledged = Earlier,
                Comment = comment,
            };
            context.ResourceTypeFileAcknowledgements.Add(acknowledgement);
            await context.SaveChangesAsync();
            return acknowledgement;
        }

        /// <summary>En vakt for brukeren på en oppgave av vakttypen som starter <paramref name="startLocal"/> (norsk tid).</summary>
        private static async Task SeedShift(PlannerDbContext context, int resourceTypeId, int userId, DateTime startLocal)
        {
            var resource = new EventResource { ResourceTypeId = resourceTypeId, StartTime = startLocal, EndTime = startLocal.AddHours(4), ShiftCount = 2 };
            context.Events.Add(new Event { Name = UniqueName("Event"), StartTime = startLocal, EndTime = startLocal.AddHours(4), Resources = [resource] });
            await context.SaveChangesAsync();
            context.Shifts.Add(new EventResourceUser { EventResourceId = resource.EventResourceId, UserId = userId });
            await context.SaveChangesAsync();
        }

        private static IFormFile PdfFile(string fileName = "instruks.pdf")
        {
            var bytes = Encoding.UTF8.GetBytes("%PDF-1.4 test");
            return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = "application/pdf",
            };
        }

        private async Task<ResourceTypeFile> GetFile(int fileId)
        {
            using var verify = _fixture.CreateContext();
            return await verify.ResourceTypeFiles.AsNoTracking().SingleAsync(f => f.ResourceTypeFileId == fileId);
        }

        private async Task<List<ResourceTypeFileAcknowledgement>> GetAcknowledgements(int fileId)
        {
            using var verify = _fixture.CreateContext();
            return await verify.ResourceTypeFileAcknowledgements.AsNoTracking().Where(a => a.ResourceTypeFileId == fileId).ToListAsync();
        }

        #region Opplasting og metadata

        [Fact]
        public async Task Upload_WithFlags_PersistsFile_AndSavesToStorage()
        {
            using var seed = _fixture.CreateContext();
            var admin = await SeedUser(seed, "Kari", "Admin", isAdmin: true);
            var resourceType = await SeedResourceType(seed);

            using var context = _fixture.CreateContext();
            var result = await CreateService(context, admin.UserId, isAdmin: true).Upload(resourceType.ResourceTypeId, new FileUploadRequest
            {
                File = PdfFile(),
                Description = "Heisførerinstruks",
                RequiresAcknowledgement = true,
                IsMandatory = true,
            });

            Assert.Equal("Heisførerinstruks", result.Description);
            Assert.Equal("instruks.pdf", result.FileName);
            Assert.Equal("application/pdf", result.MimeType);
            Assert.True(result.RequiresAcknowledgement);
            Assert.True(result.IsMandatory);
            Assert.Null(result.AcknowledgedAt);
            Assert.Null(result.Archived);
            Assert.Equal("2026-10-11T12:00:00Z", result.Created);
            Assert.Equal("Kari Admin", result.CreatedBy);
            Assert.Equal("Kari Admin", result.UpdatedBy);

            var stored = await GetFile(result.Id);
            Assert.True(stored.RequiresAcknowledgement);
            Assert.True(stored.IsMandatory);
            Assert.Equal(Now.UtcDateTime, stored.Created);
            await _storage.Received(1).Save($"resourceTypes/2026/10/{stored.StorageName}", Arg.Any<Stream>());
        }

        [Fact]
        public async Task Upload_WithoutFlags_IsPlainAttachment()
        {
            using var seed = _fixture.CreateContext();
            var admin = await SeedUser(seed, isAdmin: true);
            var resourceType = await SeedResourceType(seed);

            using var context = _fixture.CreateContext();
            var result = await CreateService(context, admin.UserId, isAdmin: true).Upload(resourceType.ResourceTypeId, new FileUploadRequest
            {
                File = PdfFile("kart.pdf"),
                Description = "Løypekart",
            });

            Assert.False(result.RequiresAcknowledgement);
            Assert.False(result.IsMandatory);
        }

        [Fact]
        public async Task Upload_Rejects_MandatoryWithoutAcknowledgement()
        {
            using var seed = _fixture.CreateContext();
            var admin = await SeedUser(seed, isAdmin: true);
            var resourceType = await SeedResourceType(seed);

            using var context = _fixture.CreateContext();
            var ex = await Assert.ThrowsAsync<DomainValidationException>(() => CreateService(context, admin.UserId, isAdmin: true)
                .Upload(resourceType.ResourceTypeId, new FileUploadRequest { File = PdfFile(), Description = "Feil", IsMandatory = true }));

            Assert.Equal(AttachmentService.MandatoryRequiresAcknowledgementMessage, ex.Message);
            await _storage.DidNotReceiveWithAnyArgs().Save(default!, default!);
        }

        [Fact]
        public async Task Upload_ThrowsEntityNotFound_ForUnknownResourceType_BeforeSavingBlob()
        {
            using var seed = _fixture.CreateContext();
            var admin = await SeedUser(seed, isAdmin: true);

            using var context = _fixture.CreateContext();
            await Assert.ThrowsAsync<EntityNotFoundException>(() => CreateService(context, admin.UserId, isAdmin: true)
                .Upload(int.MaxValue, new FileUploadRequest { File = PdfFile(), Description = "Instruks" }));

            await _storage.DidNotReceiveWithAnyArgs().Save(default!, default!);
        }

        [Fact]
        public async Task Upload_Forbidden_ForNonAdmin()
        {
            using var seed = _fixture.CreateContext();
            var user = await SeedUser(seed);
            var resourceType = await SeedResourceType(seed);

            using var context = _fixture.CreateContext();
            await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateService(context, user.UserId)
                .Upload(resourceType.ResourceTypeId, new FileUploadRequest { File = PdfFile(), Description = "Instruks" }));
        }

        [Fact]
        public async Task UpdateMetadata_ChangesDescriptionAndFlags_KeepsAcknowledgements()
        {
            using var seed = _fixture.CreateContext();
            var admin = await SeedUser(seed, "Kari", "Admin", isAdmin: true);
            var user = await SeedUser(seed);
            var resourceType = await SeedResourceType(seed);
            var file = await SeedFile(seed, resourceType.ResourceTypeId, admin.UserId, requiresAcknowledgement: true, isMandatory: true);
            await SeedAcknowledgement(seed, file.ResourceTypeFileId, user.UserId);

            // Skrur av kvittering: kvitteringen beholdes, så den gjelder igjen når flagget skrus på.
            using var context = _fixture.CreateContext();
            var service = CreateService(context, admin.UserId, isAdmin: true);
            var off = await service.UpdateMetadata(resourceType.ResourceTypeId, file.ResourceTypeFileId,
                new FileMetadataRequest { Description = "Ny beskrivelse", RequiresAcknowledgement = false, IsMandatory = false });

            Assert.Equal("Ny beskrivelse", off.Description);
            Assert.False(off.RequiresAcknowledgement);
            Assert.False(off.IsMandatory);
            Assert.Equal("2026-10-11T12:00:00Z", off.Updated);
            Assert.Equal("Kari Admin", off.UpdatedBy);
            Assert.Single(await GetAcknowledgements(file.ResourceTypeFileId));

            var on = await service.UpdateMetadata(resourceType.ResourceTypeId, file.ResourceTypeFileId,
                new FileMetadataRequest { Description = "Ny beskrivelse", RequiresAcknowledgement = true, IsMandatory = true });
            Assert.True(on.IsMandatory);
            Assert.Single(await GetAcknowledgements(file.ResourceTypeFileId));

            var ex = await Assert.ThrowsAsync<DomainValidationException>(() => service.UpdateMetadata(resourceType.ResourceTypeId, file.ResourceTypeFileId,
                new FileMetadataRequest { Description = "Ugyldig", RequiresAcknowledgement = false, IsMandatory = true }));
            Assert.Equal(AttachmentService.MandatoryRequiresAcknowledgementMessage, ex.Message);
            Assert.True((await GetFile(file.ResourceTypeFileId)).RequiresAcknowledgement);
        }

        [Fact]
        public async Task UpdateMetadata_ThrowsEntityNotFound_WhenFileIsOnAnotherResourceType()
        {
            using var seed = _fixture.CreateContext();
            var admin = await SeedUser(seed, isAdmin: true);
            var resourceType = await SeedResourceType(seed);
            var other = await SeedResourceType(seed);
            var file = await SeedFile(seed, resourceType.ResourceTypeId, admin.UserId);

            using var context = _fixture.CreateContext();
            await Assert.ThrowsAsync<EntityNotFoundException>(() => CreateService(context, admin.UserId, isAdmin: true)
                .UpdateMetadata(other.ResourceTypeId, file.ResourceTypeFileId, new FileMetadataRequest { Description = "X" }));
        }

        #endregion

        #region Arkivering og nedlasting

        [Fact]
        public async Task Archive_HidesFile_KeepsAcknowledgementsAndBlob()
        {
            using var seed = _fixture.CreateContext();
            var admin = await SeedUser(seed, isAdmin: true);
            var user = await SeedUser(seed);
            var resourceType = await SeedResourceType(seed);
            var file = await SeedFile(seed, resourceType.ResourceTypeId, admin.UserId, requiresAcknowledgement: true, isMandatory: true);
            await SeedAcknowledgement(seed, file.ResourceTypeFileId, user.UserId, "Lest");

            using var context = _fixture.CreateContext();
            var service = CreateService(context, admin.UserId, isAdmin: true);
            await service.Archive(resourceType.ResourceTypeId, file.ResourceTypeFileId);

            var stored = await GetFile(file.ResourceTypeFileId);
            Assert.Equal(Now.UtcDateTime, stored.Archived);
            Assert.Equal(admin.UserId, stored.ArchivedBy);
            Assert.Equal("Lest", Assert.Single(await GetAcknowledgements(file.ResourceTypeFileId)).Comment);

            // Borte fra vakttypen, med i arkivlisten, og sperrer ikke lenger.
            using var readContext = _fixture.CreateContext();
            var resourceTypeResponse = (await new ResourceReader(readContext, new FakeTimeProvider(Now)).GetResourceType(resourceType.ResourceTypeId))!;
            Assert.Empty(resourceTypeResponse.Files);
            var archived = Assert.Single(await service.GetArchived(resourceType.ResourceTypeId));
            Assert.Equal(file.ResourceTypeFileId, archived.Id);
            Assert.Equal("2026-10-11T12:00:00Z", archived.Archived);
            Assert.Empty(await new AttachmentRepository(readContext).GetUnacknowledgedMandatory(admin.UserId, resourceType.ResourceTypeId));

            // Idempotent: arkiveringstidspunktet står.
            await CreateService(context, admin.UserId, isAdmin: true, now: Now.AddDays(1)).Archive(resourceType.ResourceTypeId, file.ResourceTypeFileId);
            Assert.Equal(Now.UtcDateTime, (await GetFile(file.ResourceTypeFileId)).Archived);
        }

        [Fact]
        public async Task GetArchived_NewestFirst_OnlyAdmin()
        {
            using var seed = _fixture.CreateContext();
            var admin = await SeedUser(seed, isAdmin: true);
            var user = await SeedUser(seed);
            var resourceType = await SeedResourceType(seed);
            var older = await SeedFile(seed, resourceType.ResourceTypeId, admin.UserId, "Eldre", archived: Earlier);
            var newer = await SeedFile(seed, resourceType.ResourceTypeId, admin.UserId, "Nyere", archived: Earlier.AddDays(1));
            await SeedFile(seed, resourceType.ResourceTypeId, admin.UserId, "Aktiv");

            using var context = _fixture.CreateContext();
            var result = (await CreateService(context, admin.UserId, isAdmin: true).GetArchived(resourceType.ResourceTypeId)).ToList();

            Assert.Equal([newer.ResourceTypeFileId, older.ResourceTypeFileId], result.Select(f => f.Id));
            await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateService(context, user.UserId).GetArchived(resourceType.ResourceTypeId));
        }

        [Fact]
        public async Task Download_ReadsFromStorage_ArchivedOnlyForAdmin()
        {
            using var seed = _fixture.CreateContext();
            var admin = await SeedUser(seed, isAdmin: true);
            var user = await SeedUser(seed);
            var resourceType = await SeedResourceType(seed);
            var active = await SeedFile(seed, resourceType.ResourceTypeId, admin.UserId, "Aktiv");
            var archived = await SeedFile(seed, resourceType.ResourceTypeId, admin.UserId, "Arkivert", archived: Earlier);

            using var context = _fixture.CreateContext();
            var asUser = CreateService(context, user.UserId);
            var asAdmin = CreateService(context, admin.UserId, isAdmin: true);

            var download = await asUser.Download(resourceType.ResourceTypeId, active.ResourceTypeFileId);
            Assert.Equal("instruks.pdf", download.FileName);
            Assert.Equal("application/pdf", download.MimeType);
            Assert.Equal("%PDF", Encoding.UTF8.GetString(download.Data));
            await _storage.Received(1).Read($"resourceTypes/2026/09/{active.StorageName}");

            await Assert.ThrowsAsync<EntityNotFoundException>(() => asUser.Download(resourceType.ResourceTypeId, archived.ResourceTypeFileId));
            Assert.Equal("instruks.pdf", (await asAdmin.Download(resourceType.ResourceTypeId, archived.ResourceTypeFileId)).FileName);
            await Assert.ThrowsAsync<EntityNotFoundException>(() => asAdmin.Download(resourceType.ResourceTypeId, int.MaxValue));
        }

        #endregion

        #region Kvittering

        [Fact]
        public async Task Acknowledge_IsIdempotent_AndKeepsFirstComment()
        {
            using var seed = _fixture.CreateContext();
            var admin = await SeedUser(seed, isAdmin: true);
            var user = await SeedUser(seed);
            var resourceType = await SeedResourceType(seed);
            var file = await SeedFile(seed, resourceType.ResourceTypeId, admin.UserId);

            using var context = _fixture.CreateContext();
            var first = await CreateService(context, user.UserId).Acknowledge(resourceType.ResourceTypeId, file.ResourceTypeFileId, new AcknowledgeRequest { Comment = "  Lest og forstått  " });
            var second = await CreateService(context, user.UserId, now: Now.AddHours(1)).Acknowledge(resourceType.ResourceTypeId, file.ResourceTypeFileId, new AcknowledgeRequest { Comment = "Ny kommentar" });

            Assert.Equal("2026-10-11T12:00:00Z", first.AcknowledgedAt);
            Assert.Equal(first.AcknowledgedAt, second.AcknowledgedAt);
            var stored = Assert.Single(await GetAcknowledgements(file.ResourceTypeFileId));
            Assert.Equal(user.UserId, stored.UserId);
            Assert.Equal("Lest og forstått", stored.Comment);
        }

        [Fact]
        public async Task Acknowledge_WithoutComment_StoresNull()
        {
            using var seed = _fixture.CreateContext();
            var admin = await SeedUser(seed, isAdmin: true);
            var user = await SeedUser(seed);
            var resourceType = await SeedResourceType(seed);
            var blank = await SeedFile(seed, resourceType.ResourceTypeId, admin.UserId, "Tom kommentar");
            var noBody = await SeedFile(seed, resourceType.ResourceTypeId, admin.UserId, "Uten body");

            using var context = _fixture.CreateContext();
            var service = CreateService(context, user.UserId);
            await service.Acknowledge(resourceType.ResourceTypeId, blank.ResourceTypeFileId, new AcknowledgeRequest { Comment = "   " });
            // POST uten body gir null-request.
            var result = await service.Acknowledge(resourceType.ResourceTypeId, noBody.ResourceTypeFileId, null);

            Assert.Null(Assert.Single(await GetAcknowledgements(blank.ResourceTypeFileId)).Comment);
            Assert.Null(Assert.Single(await GetAcknowledgements(noBody.ResourceTypeFileId)).Comment);
            Assert.Equal("2026-10-11T12:00:00Z", result.AcknowledgedAt);
        }

        [Fact]
        public async Task Acknowledge_Rejects_PlainAttachment_AndArchivedInstruction()
        {
            using var seed = _fixture.CreateContext();
            var admin = await SeedUser(seed, isAdmin: true);
            var user = await SeedUser(seed);
            var resourceType = await SeedResourceType(seed);
            var plain = await SeedFile(seed, resourceType.ResourceTypeId, admin.UserId, "Løypekart", requiresAcknowledgement: false);
            var archived = await SeedFile(seed, resourceType.ResourceTypeId, admin.UserId, "Gammel instruks", archived: Earlier);

            using var context = _fixture.CreateContext();
            var service = CreateService(context, user.UserId);

            var plainEx = await Assert.ThrowsAsync<DomainValidationException>(() => service.Acknowledge(resourceType.ResourceTypeId, plain.ResourceTypeFileId, new AcknowledgeRequest()));
            Assert.Equal(AttachmentService.NotAnInstructionMessage, plainEx.Message);
            var archivedEx = await Assert.ThrowsAsync<DomainValidationException>(() => service.Acknowledge(resourceType.ResourceTypeId, archived.ResourceTypeFileId, new AcknowledgeRequest()));
            Assert.Equal(AttachmentService.ArchivedMessage, archivedEx.Message);
            await Assert.ThrowsAsync<EntityNotFoundException>(() => service.Acknowledge(resourceType.ResourceTypeId, int.MaxValue, new AcknowledgeRequest()));

            Assert.Empty(await GetAcknowledgements(plain.ResourceTypeFileId));
            Assert.Empty(await GetAcknowledgements(archived.ResourceTypeFileId));
        }

        #endregion

        #region Rapport

        [Fact]
        public async Task GetAcknowledgementReport_DefaultPopulation_IsUsersWithShiftsThisSeason()
        {
            using var seed = _fixture.CreateContext();
            var admin = await SeedUser(seed, "Kari", "Admin", isAdmin: true);
            var onShift = await SeedUser(seed, "Ola", "Vakt");
            var onShiftAcknowledged = await SeedUser(seed, "Per", "Aamodt");
            var lastSeason = await SeedUser(seed, "Lise", "Fjor");
            var noShift = await SeedUser(seed, "Nils", "Leser");
            var inactive = await SeedUser(seed, "Ute", "Sluttet", inactive: true);
            var resourceType = await SeedResourceType(seed);
            var file = await SeedFile(seed, resourceType.ResourceTypeId, admin.UserId, isMandatory: true);

            // Sesongstart 1. juli 2026 00:00 norsk tid: en oppgave som starter da gjelder, en som startet 30. juni gjør det ikke.
            await SeedShift(seed, resourceType.ResourceTypeId, onShift.UserId, new DateTime(2026, 7, 1, 0, 0, 0));
            await SeedShift(seed, resourceType.ResourceTypeId, onShiftAcknowledged.UserId, new DateTime(2026, 9, 15, 18, 0, 0));
            await SeedShift(seed, resourceType.ResourceTypeId, lastSeason.UserId, new DateTime(2026, 6, 30, 23, 30, 0));
            await SeedShift(seed, resourceType.ResourceTypeId, inactive.UserId, new DateTime(2026, 9, 15, 18, 0, 0));
            await SeedAcknowledgement(seed, file.ResourceTypeFileId, onShiftAcknowledged.UserId, "Ok");
            await SeedAcknowledgement(seed, file.ResourceTypeFileId, noShift.UserId);

            using var context = _fixture.CreateContext();
            var report = await CreateService(context, admin.UserId, isAdmin: true).GetAcknowledgementReport(resourceType.ResourceTypeId, file.ResourceTypeFileId, all: false);

            Assert.Equal(file.ResourceTypeFileId, report.File.Id);
            Assert.True(report.File.IsMandatory);
            Assert.Equal(2, report.Total);
            Assert.Equal(1, report.AcknowledgedCount);
            // Ikke kvittert først.
            Assert.Collection(report.Users,
                u =>
                {
                    Assert.Equal(onShift.UserId, u.UserId);
                    Assert.Equal("Ola Vakt", u.FullName);
                    Assert.Null(u.AcknowledgedAt);
                    Assert.Null(u.Comment);
                    Assert.True(u.AppliesTo);
                },
                u =>
                {
                    Assert.Equal(onShiftAcknowledged.UserId, u.UserId);
                    Assert.Equal("2026-09-01T08:00:00Z", u.AcknowledgedAt);
                    Assert.Equal("Ok", u.Comment);
                    Assert.True(u.AppliesTo);
                });
        }

        [Fact]
        public async Task GetAcknowledgementReport_All_IncludesEveryActiveUser_WithAppliesTo()
        {
            using var seed = _fixture.CreateContext();
            var admin = await SeedUser(seed, "Kari", "Admin", isAdmin: true);
            var onShift = await SeedUser(seed, "Ola", "Vakt");
            var noShift = await SeedUser(seed, "Nils", "Leser");
            var inactive = await SeedUser(seed, "Ute", "Sluttet", inactive: true);
            var resourceType = await SeedResourceType(seed);
            var file = await SeedFile(seed, resourceType.ResourceTypeId, admin.UserId);
            await SeedShift(seed, resourceType.ResourceTypeId, onShift.UserId, new DateTime(2026, 9, 15, 18, 0, 0));
            await SeedAcknowledgement(seed, file.ResourceTypeFileId, noShift.UserId, "Lest i forkant");

            using var context = _fixture.CreateContext();
            var report = await CreateService(context, admin.UserId, isAdmin: true).GetAcknowledgementReport(resourceType.ResourceTypeId, file.ResourceTypeFileId, all: true);

            // Databasen deles med andre tester, så vi sjekker våre brukere og tellingen relativt.
            var users = report.Users.ToList();
            Assert.Equal(users.Count, report.Total);
            Assert.Equal(users.Count(u => u.AcknowledgedAt is not null), report.AcknowledgedCount);
            Assert.DoesNotContain(users, u => u.UserId == inactive.UserId);

            var ola = Assert.Single(users, u => u.UserId == onShift.UserId);
            Assert.True(ola.AppliesTo);
            Assert.Null(ola.AcknowledgedAt);
            var nils = Assert.Single(users, u => u.UserId == noShift.UserId);
            Assert.False(nils.AppliesTo);
            Assert.Equal("Lest i forkant", nils.Comment);
            var kari = Assert.Single(users, u => u.UserId == admin.UserId);
            Assert.False(kari.AppliesTo);

            // Alle uten kvittering kommer før alle med.
            var lastUnacknowledged = users.FindLastIndex(u => u.AcknowledgedAt is null);
            var firstAcknowledged = users.FindIndex(u => u.AcknowledgedAt is not null);
            Assert.True(lastUnacknowledged < firstAcknowledged);
        }

        [Fact]
        public async Task GetAcknowledgementReport_Forbidden_ForNonAdmin()
        {
            using var seed = _fixture.CreateContext();
            var admin = await SeedUser(seed, isAdmin: true);
            var user = await SeedUser(seed);
            var resourceType = await SeedResourceType(seed);
            var file = await SeedFile(seed, resourceType.ResourceTypeId, admin.UserId);

            using var context = _fixture.CreateContext();
            await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateService(context, user.UserId)
                .GetAcknowledgementReport(resourceType.ResourceTypeId, file.ResourceTypeFileId, all: false));
        }

        #endregion

        #region Instruksliste

        [Fact]
        public async Task GetMyInstructions_GroupsByResourceType_WithAppliesToMeAcrossSeasonBoundary()
        {
            using var seed = _fixture.CreateContext();
            var admin = await SeedUser(seed, isAdmin: true);
            var user = await SeedUser(seed);
            var applies = await SeedResourceType(seed, UniqueName("Zz Storheis"));
            var lastSeason = await SeedResourceType(seed, UniqueName("Aa Barneheis"));
            var withoutInstructions = await SeedResourceType(seed, UniqueName("Kiosk"));
            var inactiveType = await SeedResourceType(seed, UniqueName("Nedlagt"));

            var acknowledged = await SeedFile(seed, applies.ResourceTypeId, admin.UserId, "Sikkerhetsrutine");
            var unread = await SeedFile(seed, applies.ResourceTypeId, admin.UserId, "Heisførerinstruks", isMandatory: true);
            await SeedFile(seed, applies.ResourceTypeId, admin.UserId, "Løypekart", requiresAcknowledgement: false);
            await SeedFile(seed, applies.ResourceTypeId, admin.UserId, "Arkivert", archived: Earlier);
            var lastSeasonFile = await SeedFile(seed, lastSeason.ResourceTypeId, admin.UserId, "Barneheisinstruks");
            await SeedFile(seed, withoutInstructions.ResourceTypeId, admin.UserId, "Prisliste", requiresAcknowledgement: false);
            await SeedFile(seed, inactiveType.ResourceTypeId, admin.UserId, "Gammel instruks");
            inactiveType.Inactive = true;
            await seed.SaveChangesAsync();

            // Vakt akkurat ved sesongstart (1. juli 00:00 norsk tid) gjelder; vakt 30. juni gjør det ikke.
            await SeedShift(seed, applies.ResourceTypeId, user.UserId, new DateTime(2026, 7, 1, 0, 0, 0));
            await SeedShift(seed, lastSeason.ResourceTypeId, user.UserId, new DateTime(2026, 6, 30, 23, 30, 0));
            await SeedAcknowledgement(seed, acknowledged.ResourceTypeFileId, user.UserId);

            using var context = _fixture.CreateContext();
            var all = (await CreateService(context, user.UserId).GetMyInstructions(user.UserId)).ToList();

            int[] ours = [applies.ResourceTypeId, lastSeason.ResourceTypeId, withoutInstructions.ResourceTypeId, inactiveType.ResourceTypeId];
            var groups = all.Where(g => ours.Contains(g.ResourceTypeId)).ToList();
            Assert.Collection(groups,
                g =>
                {
                    Assert.Equal(applies.ResourceTypeId, g.ResourceTypeId);
                    Assert.Equal(applies.Name, g.ResourceTypeName);
                    Assert.True(g.AppliesToMe);
                    Assert.Collection(g.Files,
                        f =>
                        {
                            Assert.Equal(unread.ResourceTypeFileId, f.Id);
                            Assert.True(f.IsMandatory);
                            Assert.Null(f.AcknowledgedAt);
                        },
                        f =>
                        {
                            Assert.Equal(acknowledged.ResourceTypeFileId, f.Id);
                            Assert.Equal("2026-09-01T08:00:00Z", f.AcknowledgedAt);
                        });
                },
                g =>
                {
                    Assert.Equal(lastSeason.ResourceTypeId, g.ResourceTypeId);
                    Assert.False(g.AppliesToMe);
                    Assert.Equal(lastSeasonFile.ResourceTypeFileId, Assert.Single(g.Files).Id);
                });

            // Gruppene som gjelder brukeren kommer før de andre, uansett navn.
            var firstNotApplying = all.FindIndex(g => !g.AppliesToMe);
            Assert.True(firstNotApplying < 0 || all.Skip(firstNotApplying).All(g => !g.AppliesToMe));
        }

        #endregion
    }
}
