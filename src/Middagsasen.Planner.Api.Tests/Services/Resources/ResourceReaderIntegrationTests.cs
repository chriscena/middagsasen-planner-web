using System.Text.Json;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services;
using Middagsasen.Planner.Api.Services.Events;
using Middagsasen.Planner.Api.Services.Resources;
using Middagsasen.Planner.Api.Services.ResourceTypes;
using Middagsasen.Planner.Api.Tests.Infrastructure;

namespace Middagsasen.Planner.Api.Tests.Services.Resources
{
    /// <summary>Integrasjonstester mot <see cref="IResourceReader"/> med ekte database.</summary>
    [Collection("Database")]
    public class ResourceReaderIntegrationTests
    {
        // Ressursen er 15.01.2026 09:00–15:00 norsk tid; «nå» er før den.
        private static readonly DateTime ResourceStart = new(2026, 1, 15, 9, 0, 0);
        private static readonly DateTime ResourceEnd = new(2026, 1, 15, 15, 0, 0);
        private static readonly TimeProvider Clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

        // UTC-tidspunkter (lagres uten sone).
        private static readonly DateTime FileCreated = new(2026, 1, 10, 8, 30, 0);
        private static readonly DateTime FileUpdated = new(2026, 1, 11, 9, 45, 0);
        private static readonly DateTime TrainingConfirmed = new(2026, 1, 12, 10, 15, 0);
        private static readonly DateTime MessageCreated = new(2026, 1, 13, 11, 20, 0);

        private readonly DatabaseFixture _fixture;

        public ResourceReaderIntegrationTests(DatabaseFixture fixture)
        {
            _fixture = fixture;
        }

        private static string UniqueName(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

        private sealed record Seeded(
            User Trainer, User Viewer, User Other, ResourceType ResourceType, ResourceTypeFile File,
            Event Event, EventResource Resource, EventResourceMessage Message, ResourceTypeTraining ConfirmedTraining,
            EventTemplate Template);

        /// <summary>
        /// En ressurstype med trener, fil (med opprettet/endret av) og opplæring, brukt av et arrangement og en mal.
        /// <c>Viewer</c> står på vakta og har bedt om opplæring; <c>Other</c> har fått opplæringen bekreftet av treneren.
        /// </summary>
        private async Task<Seeded> Seed()
        {
            using var context = _fixture.CreateContext();

            User NewUser(string firstName, string lastName) => new()
            {
                UserName = UniqueName("user"),
                FirstName = firstName,
                LastName = lastName,
                Created = DateTime.UtcNow,
            };

            var trainer = NewUser("Trener", "Person");
            var creator = NewUser("Fil", "Oppretter");
            var updater = NewUser("Fil", "Oppdaterer");
            var viewer = NewUser("Vakt", "Tar");
            var other = NewUser("Annen", "Bruker");
            context.Users.AddRange(trainer, creator, updater, viewer, other);
            await context.SaveChangesAsync();

            var resourceType = new ResourceType { Name = UniqueName("Heis"), DefaultShiftCount = 2, NotificationMessage = "Husk jakke" };
            resourceType.Trainers.Add(new ResourceTypeTrainer { UserId = trainer.UserId });
            context.ResourceTypes.Add(resourceType);
            await context.SaveChangesAsync();

            var file = new ResourceTypeFile
            {
                ResourceTypeId = resourceType.ResourceTypeId,
                StorageName = Guid.NewGuid().ToString(),
                FileName = "instruks.pdf",
                Description = "Instruks",
                MimeType = "application/pdf",
                Created = FileCreated,
                CreatedBy = creator.UserId,
                Updated = FileUpdated,
                UpdatedBy = updater.UserId,
            };
            context.ResourceTypeFiles.Add(file);

            var requested = new ResourceTypeTraining { UserId = viewer.UserId, ResourceTypeId = resourceType.ResourceTypeId, TrainingComplete = false };
            var confirmed = new ResourceTypeTraining
            {
                UserId = other.UserId,
                ResourceTypeId = resourceType.ResourceTypeId,
                TrainingComplete = true,
                Confirmed = TrainingConfirmed,
                ConfirmedBy = trainer.UserId,
            };
            context.ResourceTypeTrainings.AddRange(requested, confirmed);

            var resource = new EventResource
            {
                ResourceTypeId = resourceType.ResourceTypeId,
                StartTime = ResourceStart,
                EndTime = ResourceEnd,
                ShiftCount = 2,
            };
            var evt = new Event { Name = UniqueName("Event"), StartTime = ResourceStart, EndTime = ResourceEnd, Resources = [resource] };
            context.Events.Add(evt);

            var template = new EventTemplate
            {
                Name = UniqueName("Mal"),
                EventName = UniqueName("Event"),
                StartTime = ResourceStart,
                EndTime = ResourceEnd,
                ResourceTemplates =
                [
                    new ResourceTemplate { ResourceTypeId = resourceType.ResourceTypeId, StartTime = ResourceStart, EndTime = ResourceEnd, ShiftCount = 2 },
                ],
            };
            context.EventTemplates.Add(template);
            await context.SaveChangesAsync();

            context.Shifts.Add(new EventResourceUser
            {
                EventResourceId = resource.EventResourceId,
                UserId = viewer.UserId,
                StartTime = ResourceStart,
                EndTime = ResourceEnd,
            });
            var message = new EventResourceMessage
            {
                EventResourceId = resource.EventResourceId,
                Message = "Hei",
                Created = MessageCreated,
                CreatedBy = trainer.UserId,
            };
            context.Messages.Add(message);
            await context.SaveChangesAsync();

            return new Seeded(trainer, viewer, other, resourceType, file, evt, resource, message, confirmed, template);
        }

        private static string Json(object? value) => JsonSerializer.Serialize(value);

        [Fact]
        public async Task ResourceType_IsIdentical_ViaEvent_Template_AndResourceType()
        {
            var seeded = await Seed();
            var actor = new Actor(seeded.Viewer.UserId, IsAdmin: false);

            using var context = _fixture.CreateContext();
            var reader = new ResourceReader(context, Clock);

            var viaEvent = (await reader.GetEvent(actor, seeded.Event.EventId))!.Resources.Single().ResourceType;
            var viaResource = (await reader.GetResource(actor, seeded.Resource.EventResourceId))!.ResourceType;
            var viaResourceType = (await reader.GetResourceType(seeded.ResourceType.ResourceTypeId))!;
            var viaIds = (await reader.GetResourceTypes([seeded.ResourceType.ResourceTypeId]))[seeded.ResourceType.ResourceTypeId];
            var viaTemplate = (await new EventTemplatesService(context, reader).GetEventTemplateById(seeded.Template.EventTemplateId))
                .ResourceTemplates!.Single().ResourceType;

            Assert.True(viaResourceType.HasTraining);
            Assert.Equal("Husk jakke", viaResourceType.NotificationMessage);
            var trainer = Assert.Single(viaResourceType.Trainers);
            Assert.Equal(seeded.Trainer.UserId, trainer.UserId);
            Assert.Equal("Trener Person", trainer.FullName);
            Assert.Equal(seeded.Trainer.UserName, trainer.PhoneNo);
            var file = Assert.Single(viaResourceType.Files);
            Assert.Equal(seeded.File.ResourceTypeFileId, file.Id);
            Assert.Equal("Fil Oppretter", file.CreatedBy);
            Assert.Equal("Fil Oppdaterer", file.UpdatedBy);

            var expected = Json(viaResourceType);
            Assert.Equal(expected, Json(viaEvent));
            Assert.Equal(expected, Json(viaResource));
            Assert.Equal(expected, Json(viaIds));
            Assert.Equal(expected, Json(viaTemplate));
        }

        [Fact]
        public async Task DateFormats_LocalTimesWithoutZone_UtcTimesWithZ()
        {
            var seeded = await Seed();
            var actor = new Actor(seeded.Viewer.UserId, IsAdmin: false);

            using var context = _fixture.CreateContext();
            var reader = new ResourceReader(context, Clock);

            var evt = (await reader.GetEvent(actor, seeded.Event.EventId))!;
            var resource = evt.Resources.Single();
            var shift = resource.Shifts.Single();

            // Lokal norsk tid uten sone.
            Assert.Equal("2026-01-15T09:00", evt.StartTime);
            Assert.Equal("2026-01-15T15:00", evt.EndTime);
            Assert.Equal("2026-01-15T09:00", resource.StartTime);
            Assert.Equal("2026-01-15T15:00", resource.EndTime);
            Assert.Equal("2026-01-15T09:00", shift.StartTime);
            Assert.Equal("2026-01-15T15:00", shift.EndTime);

            // UTC-tidspunkter med Z.
            var file = resource.ResourceType.Files.Single();
            Assert.Equal("2026-01-10T08:30:00Z", file.Created);
            Assert.Equal("2026-01-11T09:45:00Z", file.Updated);
            Assert.Equal("2026-01-13T11:20:00Z", resource.Messages.Single().Created);
            Assert.Equal("2026-01-13T11:20:00Z", (await reader.GetMessage(seeded.Message.EventResourceMessageId))!.Created);
            Assert.Equal("2026-01-13T11:20:00Z", (await reader.GetMessages(seeded.Resource.EventResourceId)).Single().Created);
            Assert.Equal("2026-01-10T08:30:00Z", (await reader.GetFile(seeded.File.ResourceTypeFileId))!.Created);

            var training = (await reader.GetTraining(seeded.ConfirmedTraining.ResourceTypeTrainingId))!;
            Assert.Equal("2026-01-12T10:15:00Z", training.Confirmed);
            Assert.Equal("Trener Person", training.ConfirmedByName);
            Assert.Equal(seeded.ResourceType.Name, training.ResourceTypeName);
        }

        [Fact]
        public async Task GetEvents_GivesFlagsForActor()
        {
            var seeded = await Seed();

            using var context = _fixture.CreateContext();
            var reader = new ResourceReader(context, Clock);

            async Task<ResourceResponse> ResourceFor(Actor actor)
            {
                var events = await reader.GetEvents(actor, ResourceStart, ResourceStart.AddMinutes(1));
                return events.Single(e => e.Id == seeded.Event.EventId).Resources.Single();
            }

            // Vakttakeren: står på vakta og har bedt om opplæring.
            var forViewer = await ResourceFor(new Actor(seeded.Viewer.UserId, IsAdmin: false));
            Assert.True(forViewer.IsMissingStaff);
            Assert.False(forViewer.IsFull);
            Assert.False(forViewer.IsPast);
            Assert.False(forViewer.CanSignUp); // står allerede på vakta
            Assert.False(forViewer.MustAnswerTraining); // har opplæringsrad
            var viewerShift = forViewer.Shifts.Single();
            Assert.True(viewerShift.IsMine);
            Assert.True(viewerShift.NeedsTraining);
            Assert.True(viewerShift.CanEdit);
            Assert.True(viewerShift.CanWithdraw);
            Assert.False(viewerShift.CanConfirmTraining);

            // Treneren: kan ta vakt, må svare på opplæring og kan bekrefte opplæringen.
            var forTrainer = await ResourceFor(new Actor(seeded.Trainer.UserId, IsAdmin: false));
            Assert.True(forTrainer.CanSignUp);
            Assert.True(forTrainer.MustAnswerTraining);
            var trainerView = forTrainer.Shifts.Single();
            Assert.False(trainerView.IsMine);
            Assert.False(trainerView.CanEdit);
            Assert.True(trainerView.CanConfirmTraining);

            // Utenfor tidsrommet.
            Assert.DoesNotContain(
                await reader.GetEvents(new Actor(seeded.Viewer.UserId, IsAdmin: false), ResourceStart.AddMinutes(1), ResourceStart.AddDays(1)),
                e => e.Id == seeded.Event.EventId);
        }

        [Fact]
        public async Task ReturnsNull_WhenNotFound()
        {
            using var context = _fixture.CreateContext();
            var reader = new ResourceReader(context, Clock);
            var actor = new Actor(1, IsAdmin: false);

            Assert.Null(await reader.GetEvent(actor, int.MaxValue));
            Assert.Null(await reader.GetResource(actor, int.MaxValue));
            Assert.Null(await reader.GetResourceType(int.MaxValue));
            Assert.Null(await reader.GetTraining(int.MaxValue));
            Assert.Null(await reader.GetMessage(int.MaxValue));
            Assert.Null(await reader.GetFile(int.MaxValue));
            Assert.Empty(await reader.GetResourceTypes([int.MaxValue]));
        }
    }
}
