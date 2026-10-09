using Microsoft.EntityFrameworkCore;
using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services;
using Middagsasen.Planner.Api.Services.Events;
using Middagsasen.Planner.Api.Services.Resources;
using Middagsasen.Planner.Api.Tests.Infrastructure;
using NSubstitute;

namespace Middagsasen.Planner.Api.Tests.Services.Events
{
    /// <summary>Integrasjonstester for anleggskrav (#155) på vaktlister og maler, og advarslene i <see cref="EventResponse"/>.</summary>
    [Collection("Database")]
    public class FacilityRequirementsIntegrationTests
    {
        // Fast «nå» (UTC) før vaktlistene i januar 2026.
        private static readonly DateTime UtcNow = new(2026, 1, 1, 12, 0, 0);
        private static readonly TimeProvider Clock = new FakeTimeProvider(new DateTimeOffset(UtcNow, TimeSpan.Zero));

        private static readonly DateTime Day = new(2026, 1, 15);
        private static DateTime At(int hour) => Day.AddHours(hour);

        private readonly DatabaseFixture _fixture;

        public FacilityRequirementsIntegrationTests(DatabaseFixture fixture)
        {
            _fixture = fixture;
        }

        private static string UniqueName(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

        private static EventsService CreateEventsService(PlannerDbContext context)
        {
            var currentUser = Substitute.For<ICurrentUserService>();
            currentUser.UserId.Returns(1);
            currentUser.IsAdmin.Returns(true);
            return new EventsService(context, new ResourceReader(context, Clock), currentUser);
        }

        private static EventTemplatesService CreateTemplatesService(PlannerDbContext context)
            => new(context, new ResourceReader(context, Clock));

        private static async Task<Competency> SeedCompetency(PlannerDbContext context, string? name = null, bool inactive = false)
        {
            var competency = new Competency { Name = name ?? UniqueName("Kompetanse"), Inactive = inactive };
            context.Competencies.Add(competency);
            await context.SaveChangesAsync();
            return competency;
        }

        private static async Task<ResourceType> SeedResourceType(PlannerDbContext context)
        {
            var rt = new ResourceType { Name = UniqueName("RT"), DefaultStaff = 1 };
            context.ResourceTypes.Add(rt);
            await context.SaveChangesAsync();
            return rt;
        }

        private static async Task<User> SeedUser(
            PlannerDbContext context, Competency? competency = null, bool approved = true, DateTime? expiry = null)
        {
            var user = new User { UserName = UniqueName("user"), FirstName = "Test", LastName = "User", Created = DateTime.UtcNow };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            if (competency is not null)
            {
                context.UserCompetencies.Add(new UserCompetency
                {
                    UserId = user.UserId,
                    CompetencyId = competency.CompetencyId,
                    Approved = approved,
                    ExpiryDate = expiry,
                    Created = DateTime.UtcNow,
                });
                await context.SaveChangesAsync();
            }

            return user;
        }

        private static CompetencyRequirementRequest Req(Competency competency, int minimumRequired = 1)
            => new() { CompetencyId = competency.CompetencyId, MinimumRequired = minimumRequired };

        private static EventRequest EventRequest(IEnumerable<CompetencyRequirementRequest>? requirements) => new()
        {
            Name = UniqueName("Vaktliste"),
            StartTime = At(17),
            EndTime = At(21),
            Resources = [],
            CompetencyRequirements = requirements,
        };

        private async Task<int> SeedEventWithRequirements(params (Competency Competency, int Minimum)[] requirements)
        {
            using var context = _fixture.CreateContext();
            var evt = new Event
            {
                Name = UniqueName("Vaktliste"),
                StartTime = At(17),
                EndTime = At(21),
                CompetencyRequirements = requirements
                    .Select(r => new EventCompetencyRequirement { CompetencyId = r.Competency.CompetencyId, MinimumRequired = r.Minimum })
                    .ToList(),
            };
            context.Events.Add(evt);
            await context.SaveChangesAsync();
            return evt.EventId;
        }

        private async Task<List<(int CompetencyId, int MinimumRequired)>> StoredRequirements(int eventId)
        {
            using var context = _fixture.CreateContext();
            return (await context.EventCompetencyRequirements.AsNoTracking()
                    .Where(r => r.EventId == eventId)
                    .OrderBy(r => r.CompetencyId)
                    .Select(r => new { r.CompetencyId, r.MinimumRequired })
                    .ToListAsync())
                .Select(r => (r.CompetencyId, r.MinimumRequired))
                .ToList();
        }

        #region Vaktliste: lagring

        [Fact]
        public async Task CreateEvent_WithRequirements_PersistsAndReturnsThem()
        {
            using var seed = _fixture.CreateContext();
            var snowmobile = await SeedCompetency(seed, UniqueName("B_Snøskuter"));
            var firstAid = await SeedCompetency(seed, UniqueName("A_Førstehjelp"));

            using var context = _fixture.CreateContext();
            var result = await CreateEventsService(context).CreateEvent(EventRequest([Req(snowmobile), Req(firstAid, 2)]));

            // Sortert på kompetansenavn.
            Assert.Collection(result.CompetencyRequirements,
                r =>
                {
                    Assert.Equal(firstAid.CompetencyId, r.CompetencyId);
                    Assert.Equal(firstAid.Name, r.CompetencyName);
                    Assert.Equal(2, r.MinimumRequired);
                },
                r =>
                {
                    Assert.Equal(snowmobile.CompetencyId, r.CompetencyId);
                    Assert.Equal(snowmobile.Name, r.CompetencyName);
                    Assert.Equal(1, r.MinimumRequired);
                });
            Assert.Equal(2, (await StoredRequirements(result.Id)).Count);
        }

        [Fact]
        public async Task CreateEvent_WithoutRequirements_GivesEmptyLists()
        {
            using var context = _fixture.CreateContext();
            var result = await CreateEventsService(context).CreateEvent(EventRequest(null));

            Assert.Empty(result.CompetencyRequirements);
            Assert.Empty(result.CompetencyWarnings);
        }

        [Fact]
        public async Task UpdateEvent_ReplacesRequirements_AddsChangesAndRemoves()
        {
            using var seed = _fixture.CreateContext();
            var kept = await SeedCompetency(seed);
            var removed = await SeedCompetency(seed);
            var added = await SeedCompetency(seed);
            var eventId = await SeedEventWithRequirements((kept, 1), (removed, 1));

            using var context = _fixture.CreateContext();
            var result = await CreateEventsService(context).UpdateEvent(eventId, EventRequest([Req(kept, 3), Req(added, 2)]));

            Assert.Equal(
                new[] { (kept.CompetencyId, 3), (added.CompetencyId, 2) }.OrderBy(r => r.Item1),
                result.CompetencyRequirements.Select(r => (r.CompetencyId, r.MinimumRequired)).OrderBy(r => r.Item1));
            Assert.Equal(
                new[] { (kept.CompetencyId, 3), (added.CompetencyId, 2) }.OrderBy(r => r.Item1),
                await StoredRequirements(eventId));
        }

        [Fact]
        public async Task UpdateEvent_NullRequirements_KeepsThem()
        {
            using var seed = _fixture.CreateContext();
            var competency = await SeedCompetency(seed);
            var eventId = await SeedEventWithRequirements((competency, 2));

            using var context = _fixture.CreateContext();
            var result = await CreateEventsService(context).UpdateEvent(eventId, EventRequest(null));

            var requirement = Assert.Single(result.CompetencyRequirements);
            Assert.Equal(2, requirement.MinimumRequired);
            Assert.Equal([(competency.CompetencyId, 2)], await StoredRequirements(eventId));
        }

        [Fact]
        public async Task UpdateEvent_EmptyList_RemovesAll()
        {
            using var seed = _fixture.CreateContext();
            var competency = await SeedCompetency(seed);
            var eventId = await SeedEventWithRequirements((competency, 1));

            using var context = _fixture.CreateContext();
            var result = await CreateEventsService(context).UpdateEvent(eventId, EventRequest([]));

            Assert.Empty(result.CompetencyRequirements);
            Assert.Empty(await StoredRequirements(eventId));
        }

        public static TheoryData<string> InvalidCases => new() { "duplicate", "zero", "negative", "unknown", "null", "inactive" };

        private static List<CompetencyRequirementRequest> InvalidRequirements(string invalidCase, Competency competency, Competency inactive) => invalidCase switch
        {
            "null" => [Req(competency), null!],
            "inactive" => [Req(competency), Req(inactive)],
            "duplicate" => [Req(competency), Req(competency, 2)],
            "zero" => [Req(competency, 0)],
            "negative" => [Req(competency, -1)],
            "unknown" => [new CompetencyRequirementRequest { CompetencyId = int.MaxValue, MinimumRequired = 1 }],
            _ => throw new ArgumentOutOfRangeException(nameof(invalidCase)),
        };

        [Theory]
        [MemberData(nameof(InvalidCases))]
        public async Task CreateEvent_InvalidRequirements_ThrowsDomainValidation(string invalidCase)
        {
            using var seed = _fixture.CreateContext();
            var competency = await SeedCompetency(seed);
            var inactive = await SeedCompetency(seed, inactive: true);
            var request = EventRequest(InvalidRequirements(invalidCase, competency, inactive));

            using var context = _fixture.CreateContext();
            await Assert.ThrowsAsync<DomainValidationException>(() => CreateEventsService(context).CreateEvent(request));

            using var verify = _fixture.CreateContext();
            Assert.False(await verify.Events.AnyAsync(e => e.Name == request.Name));
        }

        [Theory]
        [MemberData(nameof(InvalidCases))]
        public async Task UpdateEvent_InvalidRequirements_ThrowsDomainValidation_AndSavesNothing(string invalidCase)
        {
            using var seed = _fixture.CreateContext();
            var competency = await SeedCompetency(seed);
            var inactive = await SeedCompetency(seed, inactive: true);
            var eventId = await SeedEventWithRequirements((competency, 1));
            var request = EventRequest(InvalidRequirements(invalidCase, competency, inactive));

            using var context = _fixture.CreateContext();
            await Assert.ThrowsAsync<DomainValidationException>(() => CreateEventsService(context).UpdateEvent(eventId, request));

            Assert.Equal([(competency.CompetencyId, 1)], await StoredRequirements(eventId));
            using var verify = _fixture.CreateContext();
            Assert.NotEqual(request.Name, (await verify.Events.AsNoTracking().SingleAsync(e => e.EventId == eventId)).Name);
        }

        [Fact]
        public async Task CreateEvent_NullRequirement_GivesMissingMessage()
        {
            using var context = _fixture.CreateContext();
            var ex = await Assert.ThrowsAsync<DomainValidationException>(() =>
                CreateEventsService(context).CreateEvent(EventRequest([null!])));

            Assert.Equal(CompetencyRequirementSet.MissingRequirementMessage, ex.Message);
        }

        [Fact]
        public async Task UpdateEvent_InactiveCompetency_GivesDeletedMessage()
        {
            using var seed = _fixture.CreateContext();
            var inactive = await SeedCompetency(seed, inactive: true);
            var eventId = await SeedEventWithRequirements((inactive, 1));

            using var context = _fixture.CreateContext();
            var ex = await Assert.ThrowsAsync<DomainValidationException>(() =>
                CreateEventsService(context).UpdateEvent(eventId, EventRequest([Req(inactive)])));

            Assert.Equal(CompetencyRequirementSet.InactiveCompetencyMessage, ex.Message);
            Assert.Equal([(inactive.CompetencyId, 1)], await StoredRequirements(eventId));
        }

        [Fact]
        public async Task UpdateEvent_InactiveRequirement_IsKeptWhenNull_AndRemovedWhenResponseListIsSentBack()
        {
            using var seed = _fixture.CreateContext();
            var active = await SeedCompetency(seed);
            var deleted = await SeedCompetency(seed);
            var eventId = await SeedEventWithRequirements((active, 1), (deleted, 2));
            deleted.Inactive = true;
            await seed.SaveChangesAsync();

            // null = uendret: kravet til den slettede kompetansen blir stående i databasen, men vises ikke.
            using var keepContext = _fixture.CreateContext();
            var kept = await CreateEventsService(keepContext).UpdateEvent(eventId, EventRequest(null));
            Assert.Equal([active.CompetencyId], kept.CompetencyRequirements.Select(r => r.CompetencyId));
            Assert.Equal(
                new[] { (active.CompetencyId, 1), (deleted.CompetencyId, 2) }.OrderBy(r => r.Item1),
                await StoredRequirements(eventId));

            // Klienten sender hele listen den fikk (uten det skjulte kravet), så det fjernes.
            var sentBack = kept.CompetencyRequirements
                .Select(r => new CompetencyRequirementRequest { CompetencyId = r.CompetencyId, MinimumRequired = r.MinimumRequired })
                .ToList();
            using var saveContext = _fixture.CreateContext();
            var saved = await CreateEventsService(saveContext).UpdateEvent(eventId, EventRequest(sentBack));
            Assert.Equal([active.CompetencyId], saved.CompetencyRequirements.Select(r => r.CompetencyId));
            Assert.Equal([(active.CompetencyId, 1)], await StoredRequirements(eventId));
        }

        [Fact]
        public async Task DeleteEvent_RemovesRequirements()
        {
            using var seed = _fixture.CreateContext();
            var competency = await SeedCompetency(seed);
            var eventId = await SeedEventWithRequirements((competency, 1));

            using var context = _fixture.CreateContext();
            await CreateEventsService(context).DeleteEvent(eventId);

            Assert.Empty(await StoredRequirements(eventId));
        }

        #endregion

        #region Maler

        [Fact]
        public async Task Template_CreateAndUpdate_ReplacesRequirements_NullKeeps()
        {
            using var seed = _fixture.CreateContext();
            var first = await SeedCompetency(seed);
            var second = await SeedCompetency(seed);

            using var context = _fixture.CreateContext();
            var service = CreateTemplatesService(context);
            var created = await service.CreateEventTemplate(TemplateRequest([Req(first, 2)]));
            var createdRequirement = Assert.Single(created.CompetencyRequirements);
            Assert.Equal(first.Name, createdRequirement.CompetencyName);
            Assert.Equal(2, createdRequirement.MinimumRequired);

            using var updateContext = _fixture.CreateContext();
            var updated = await CreateTemplatesService(updateContext).UpdateEventTemplate(created.Id, TemplateRequest([Req(second, 1)]));
            var updatedRequirement = Assert.Single(updated.CompetencyRequirements);
            Assert.Equal(second.CompetencyId, updatedRequirement.CompetencyId);

            using var keepContext = _fixture.CreateContext();
            var kept = await CreateTemplatesService(keepContext).UpdateEventTemplate(created.Id, TemplateRequest(null));
            Assert.Equal(second.CompetencyId, Assert.Single(kept.CompetencyRequirements).CompetencyId);

            using var invalidContext = _fixture.CreateContext();
            await Assert.ThrowsAsync<DomainValidationException>(() =>
                CreateTemplatesService(invalidContext).UpdateEventTemplate(created.Id, TemplateRequest([Req(first, 0)])));
        }

        private static EventTemplateRequest TemplateRequest(IEnumerable<CompetencyRequirementRequest>? requirements) => new()
        {
            Name = UniqueName("Mal"),
            EventName = UniqueName("Vaktliste"),
            StartTime = new TimeOnly(17, 0),
            EndTime = new TimeOnly(21, 0),
            ResourceTemplates = [],
            CompetencyRequirements = requirements,
        };

        private async Task<EventTemplate> SeedTemplateWithRequirements(params (Competency Competency, int Minimum)[] requirements)
        {
            using var context = _fixture.CreateContext();
            var template = new EventTemplate
            {
                Name = UniqueName("Mal"),
                EventName = UniqueName("Vaktliste"),
                StartTime = new DateTime(2000, 1, 1, 17, 0, 0),
                EndTime = new DateTime(2000, 1, 1, 21, 0, 0),
                CompetencyRequirements = requirements
                    .Select(r => new EventTemplateCompetencyRequirement { CompetencyId = r.Competency.CompetencyId, MinimumRequired = r.Minimum })
                    .ToList(),
            };
            context.EventTemplates.Add(template);
            await context.SaveChangesAsync();
            return template;
        }

        private async Task<List<(int CompetencyId, int MinimumRequired)>> StoredTemplateRequirements(int templateId)
        {
            using var context = _fixture.CreateContext();
            return (await context.EventTemplateCompetencyRequirements.AsNoTracking()
                    .Where(r => r.EventTemplateId == templateId)
                    .OrderBy(r => r.CompetencyId)
                    .Select(r => new { r.CompetencyId, r.MinimumRequired })
                    .ToListAsync())
                .Select(r => (r.CompetencyId, r.MinimumRequired))
                .ToList();
        }

        [Fact]
        public async Task Template_NullRequirement_GivesMissingMessage()
        {
            var template = await SeedTemplateWithRequirements();

            using var context = _fixture.CreateContext();
            var service = CreateTemplatesService(context);

            var create = await Assert.ThrowsAsync<DomainValidationException>(() => service.CreateEventTemplate(TemplateRequest([null!])));
            Assert.Equal(CompetencyRequirementSet.MissingRequirementMessage, create.Message);

            var update = await Assert.ThrowsAsync<DomainValidationException>(() =>
                service.UpdateEventTemplate(template.EventTemplateId, TemplateRequest([null!])));
            Assert.Equal(CompetencyRequirementSet.MissingRequirementMessage, update.Message);
        }

        [Fact]
        public async Task Template_InactiveCompetency_IsRejected()
        {
            using var seed = _fixture.CreateContext();
            var inactive = await SeedCompetency(seed, inactive: true);
            var template = await SeedTemplateWithRequirements();

            using var context = _fixture.CreateContext();
            var service = CreateTemplatesService(context);

            var create = await Assert.ThrowsAsync<DomainValidationException>(() => service.CreateEventTemplate(TemplateRequest([Req(inactive)])));
            Assert.Equal(CompetencyRequirementSet.InactiveCompetencyMessage, create.Message);

            var update = await Assert.ThrowsAsync<DomainValidationException>(() =>
                service.UpdateEventTemplate(template.EventTemplateId, TemplateRequest([Req(inactive)])));
            Assert.Equal(CompetencyRequirementSet.InactiveCompetencyMessage, update.Message);
        }

        [Fact]
        public async Task Template_InactiveRequirement_IsHidden_KeptWhenNull_AndRemovedWhenResponseListIsSentBack()
        {
            using var seed = _fixture.CreateContext();
            var active = await SeedCompetency(seed);
            var deleted = await SeedCompetency(seed, inactive: true);
            var template = await SeedTemplateWithRequirements((active, 1), (deleted, 2));

            using var keepContext = _fixture.CreateContext();
            var kept = await CreateTemplatesService(keepContext).UpdateEventTemplate(template.EventTemplateId, TemplateRequest(null));
            Assert.Equal([active.CompetencyId], kept.CompetencyRequirements.Select(r => r.CompetencyId));
            Assert.Equal(2, (await StoredTemplateRequirements(template.EventTemplateId)).Count);

            var sentBack = kept.CompetencyRequirements
                .Select(r => new CompetencyRequirementRequest { CompetencyId = r.CompetencyId, MinimumRequired = r.MinimumRequired })
                .ToList();
            using var saveContext = _fixture.CreateContext();
            var saved = await CreateTemplatesService(saveContext).UpdateEventTemplate(template.EventTemplateId, TemplateRequest(sentBack));
            Assert.Equal([active.CompetencyId], saved.CompetencyRequirements.Select(r => r.CompetencyId));
            Assert.Equal([(active.CompetencyId, 1)], await StoredTemplateRequirements(template.EventTemplateId));
        }

        [Fact]
        public async Task CreateEventFromTemplate_DoesNotCopyInactiveRequirements()
        {
            using var seed = _fixture.CreateContext();
            var active = await SeedCompetency(seed);
            var deleted = await SeedCompetency(seed, inactive: true);
            var template = await SeedTemplateWithRequirements((active, 1), (deleted, 2));

            using var context = _fixture.CreateContext();
            var result = await CreateEventsService(context)
                .CreateEventFromTemplate(template.EventTemplateId, new EventFromTemplateRequest { StartDate = DateOnly.FromDateTime(Day) });

            Assert.Equal([active.CompetencyId], result.CompetencyRequirements.Select(r => r.CompetencyId));
            Assert.Equal([(active.CompetencyId, 1)], await StoredRequirements(result.Id));
        }

        [Fact]
        public async Task CreateTemplateFromEvent_DoesNotCopyInactiveRequirements()
        {
            using var seed = _fixture.CreateContext();
            var active = await SeedCompetency(seed);
            var deleted = await SeedCompetency(seed, inactive: true);
            var eventId = await SeedEventWithRequirements((active, 1), (deleted, 2));

            using var context = _fixture.CreateContext();
            var result = await CreateTemplatesService(context)
                .CreateTemplateFromEvent(eventId, new TemplateFromEventRequest { Name = UniqueName("FraVaktliste") });

            Assert.Equal([active.CompetencyId], result.CompetencyRequirements.Select(r => r.CompetencyId));
            Assert.Equal([(active.CompetencyId, 1)], await StoredTemplateRequirements(result.Id));
        }

        [Fact]
        public async Task CreateEventFromTemplate_CopiesRequirements()
        {
            using var seed = _fixture.CreateContext();
            var competency = await SeedCompetency(seed);
            var template = new EventTemplate
            {
                Name = UniqueName("Mal"),
                EventName = UniqueName("Vaktliste"),
                StartTime = new DateTime(2000, 1, 1, 17, 0, 0),
                EndTime = new DateTime(2000, 1, 1, 21, 0, 0),
                CompetencyRequirements = [new EventTemplateCompetencyRequirement { CompetencyId = competency.CompetencyId, MinimumRequired = 2 }],
            };
            seed.EventTemplates.Add(template);
            await seed.SaveChangesAsync();

            using var context = _fixture.CreateContext();
            var result = await CreateEventsService(context)
                .CreateEventFromTemplate(template.EventTemplateId, new EventFromTemplateRequest { StartDate = DateOnly.FromDateTime(Day) });

            var requirement = Assert.Single(result.CompetencyRequirements);
            Assert.Equal(competency.CompetencyId, requirement.CompetencyId);
            Assert.Equal(competency.Name, requirement.CompetencyName);
            Assert.Equal(2, requirement.MinimumRequired);
            Assert.Equal([(competency.CompetencyId, 2)], await StoredRequirements(result.Id));

            // Vaktlisten er uavhengig av malen.
            using var verify = _fixture.CreateContext();
            Assert.Single(await verify.EventTemplateCompetencyRequirements.Where(r => r.EventTemplateId == template.EventTemplateId).ToListAsync());
        }

        [Fact]
        public async Task CreateTemplateFromEvent_CopiesRequirements()
        {
            using var seed = _fixture.CreateContext();
            var competency = await SeedCompetency(seed);
            var eventId = await SeedEventWithRequirements((competency, 3));

            using var context = _fixture.CreateContext();
            var result = await CreateTemplatesService(context)
                .CreateTemplateFromEvent(eventId, new TemplateFromEventRequest { Name = UniqueName("FraVaktliste") });

            var requirement = Assert.Single(result.CompetencyRequirements);
            Assert.Equal(competency.CompetencyId, requirement.CompetencyId);
            Assert.Equal(competency.Name, requirement.CompetencyName);
            Assert.Equal(3, requirement.MinimumRequired);
        }

        #endregion

        #region Advarsler

        private sealed record WarningScenario(int EventId, Competency Competency);

        /// <summary>
        /// Issue-eksemplet: vaktliste 17–21 med anleggskrav om én snøskuterfører. <paramref name="driver"/> står i kiosken
        /// 17–19 (vaktens egne tider); en annen bruker uten kompetansen står i storheisen 18–21 (oppgavens tider, vakttider null).
        /// </summary>
        private async Task<WarningScenario> SeedIssueExample(Func<PlannerDbContext, Competency, Task<User>> driver, DateTime? day = null)
        {
            var date = day ?? Day;
            DateTime At(int hour) => date.AddHours(hour);

            using var context = _fixture.CreateContext();
            var competency = await SeedCompetency(context, UniqueName("Snøskuterfører"));
            var kiosk = await SeedResourceType(context);
            var lift = await SeedResourceType(context);
            var driverUser = await driver(context, competency);
            var liftUser = await SeedUser(context);

            var kioskResource = new EventResource { ResourceTypeId = kiosk.ResourceTypeId, StartTime = At(17), EndTime = At(21), MinimumStaff = 1 };
            var liftResource = new EventResource { ResourceTypeId = lift.ResourceTypeId, StartTime = At(18), EndTime = At(21), MinimumStaff = 1 };
            var evt = new Event
            {
                Name = UniqueName("Vaktliste"),
                StartTime = At(17),
                EndTime = At(21),
                Resources = [kioskResource, liftResource],
                CompetencyRequirements = [new EventCompetencyRequirement { CompetencyId = competency.CompetencyId, MinimumRequired = 1 }],
            };
            context.Events.Add(evt);
            await context.SaveChangesAsync();

            context.Shifts.AddRange(
                new EventResourceUser { EventResourceId = kioskResource.EventResourceId, UserId = driverUser.UserId, StartTime = At(17), EndTime = At(19) },
                new EventResourceUser { EventResourceId = liftResource.EventResourceId, UserId = liftUser.UserId });
            await context.SaveChangesAsync();

            return new WarningScenario(evt.EventId, competency);
        }

        private async Task<EventResponse> ReadEvent(int eventId)
        {
            using var context = _fixture.CreateContext();
            return (await new ResourceReader(context, Clock).GetEvent(new Actor(1, IsAdmin: false), eventId))!;
        }

        [Fact]
        public async Task GetEvent_IssueExample_GivesWarning19To21()
        {
            var scenario = await SeedIssueExample((c, competency) => SeedUser(c, competency));

            var evt = await ReadEvent(scenario.EventId);

            var warning = Assert.Single(evt.CompetencyWarnings);
            Assert.Equal(scenario.Competency.CompetencyId, warning.CompetencyId);
            Assert.Equal(scenario.Competency.Name, warning.CompetencyName);
            Assert.Equal(1, warning.MinimumRequired);
            Assert.Equal(0, warning.CurrentCount);
            Assert.Equal("2026-01-15T19:00", warning.StartTime);
            Assert.Equal("2026-01-15T21:00", warning.EndTime);
        }

        [Fact]
        public async Task GetEvent_ShiftWithoutOwnTimes_UsesResourceTimes_AndMeetsRequirement()
        {
            // Snøskuterføreren står i storheisen uten egne vakttider, altså oppgavens 18–21; kiosken 17–19 dekker resten.
            using var context = _fixture.CreateContext();
            var competency = await SeedCompetency(context);
            var rt = await SeedResourceType(context);
            var first = await SeedUser(context, competency);
            var second = await SeedUser(context, competency);
            var early = new EventResource { ResourceTypeId = rt.ResourceTypeId, StartTime = At(17), EndTime = At(19), MinimumStaff = 1 };
            var late = new EventResource { ResourceTypeId = rt.ResourceTypeId, StartTime = At(18), EndTime = At(21), MinimumStaff = 1 };
            var evt = new Event
            {
                Name = UniqueName("Vaktliste"),
                StartTime = At(17),
                EndTime = At(21),
                Resources = [early, late],
                CompetencyRequirements = [new EventCompetencyRequirement { CompetencyId = competency.CompetencyId, MinimumRequired = 1 }],
            };
            context.Events.Add(evt);
            await context.SaveChangesAsync();
            context.Shifts.AddRange(
                new EventResourceUser { EventResourceId = early.EventResourceId, UserId = first.UserId },
                new EventResourceUser { EventResourceId = late.EventResourceId, UserId = second.UserId });
            await context.SaveChangesAsync();

            var result = await ReadEvent(evt.EventId);

            Assert.Empty(result.CompetencyWarnings);
            Assert.Single(result.CompetencyRequirements);
        }

        [Fact]
        public async Task GetEvent_ExpiredCompetency_DoesNotCount()
        {
            var scenario = await SeedIssueExample((c, competency) => SeedUser(c, competency, expiry: UtcNow.AddDays(-1)));

            var warning = Assert.Single((await ReadEvent(scenario.EventId)).CompetencyWarnings);
            Assert.Equal("2026-01-15T17:00", warning.StartTime);
            Assert.Equal("2026-01-15T21:00", warning.EndTime);
            Assert.Equal(0, warning.CurrentCount);
        }

        // Vaktlisten starter 2026-01-15 17:00 norsk tid = 16:00 UTC (vintertid). «Nå» er 2026-01-01 12:00 UTC.

        [Fact]
        public async Task GetEvent_CompetencyExpiringBetweenNowAndEventStart_DoesNotCount()
        {
            var scenario = await SeedIssueExample((c, competency) => SeedUser(c, competency, expiry: new DateTime(2026, 1, 10)));

            var warning = Assert.Single((await ReadEvent(scenario.EventId)).CompetencyWarnings);
            Assert.Equal("2026-01-15T17:00", warning.StartTime);
            Assert.Equal("2026-01-15T21:00", warning.EndTime);
            Assert.Equal(0, warning.CurrentCount);
        }

        [Theory]
        [InlineData(15, 59, false)] // utløper før start
        [InlineData(16, 0, false)]  // utløper akkurat ved start (ExpiryDate <= tidspunktet)
        [InlineData(16, 1, true)]   // utløper etter start (17:01 norsk tid); ville ikke telt om 17:00 ble lest som UTC
        public async Task GetEvent_CompetencyValidity_IsEvaluatedAtEventStartInUtc(int expiryHourUtc, int expiryMinuteUtc, bool counts)
        {
            var expiry = new DateTime(2026, 1, 15, expiryHourUtc, expiryMinuteUtc, 0);
            var scenario = await SeedIssueExample((c, competency) => SeedUser(c, competency, expiry: expiry));

            var warning = Assert.Single((await ReadEvent(scenario.EventId)).CompetencyWarnings);
            Assert.Equal(counts ? "2026-01-15T19:00" : "2026-01-15T17:00", warning.StartTime);
            Assert.Equal("2026-01-15T21:00", warning.EndTime);
        }

        [Fact]
        public async Task GetEvent_PastEvent_CompetencyExpiredAfterEvent_StillCounts()
        {
            // Vaktlisten var 2025-12-15; kompetansen utløp 2025-12-20, altså etter vaktlisten, men før «nå».
            var scenario = await SeedIssueExample(
                (c, competency) => SeedUser(c, competency, expiry: new DateTime(2025, 12, 20)),
                day: new DateTime(2025, 12, 15));

            var warning = Assert.Single((await ReadEvent(scenario.EventId)).CompetencyWarnings);
            Assert.Equal("2025-12-15T19:00", warning.StartTime);
            Assert.Equal("2025-12-15T21:00", warning.EndTime);
        }

        [Fact]
        public async Task GetEvent_PastEvent_CompetencyExpiredBeforeEvent_DoesNotCount()
        {
            var scenario = await SeedIssueExample(
                (c, competency) => SeedUser(c, competency, expiry: new DateTime(2025, 12, 10)),
                day: new DateTime(2025, 12, 15));

            var warning = Assert.Single((await ReadEvent(scenario.EventId)).CompetencyWarnings);
            Assert.Equal("2025-12-15T17:00", warning.StartTime);
        }

        [Fact]
        public async Task GetEvent_InactiveCompetencyRequirement_IsHiddenFromRequirementsAndWarnings()
        {
            using var seed = _fixture.CreateContext();
            var active = await SeedCompetency(seed);
            var deleted = await SeedCompetency(seed, inactive: true);
            var eventId = await SeedEventWithRequirements((active, 1), (deleted, 1));

            var evt = await ReadEvent(eventId);

            Assert.Equal([active.CompetencyId], evt.CompetencyRequirements.Select(r => r.CompetencyId));
            Assert.Equal([active.CompetencyId], evt.CompetencyWarnings.Select(w => w.CompetencyId));
        }

        [Fact]
        public async Task GetEvent_UnapprovedCompetency_DoesNotCount()
        {
            var scenario = await SeedIssueExample((c, competency) => SeedUser(c, competency, approved: false));

            var warning = Assert.Single((await ReadEvent(scenario.EventId)).CompetencyWarnings);
            Assert.Equal("2026-01-15T17:00", warning.StartTime);
            Assert.Equal("2026-01-15T21:00", warning.EndTime);
        }

        [Fact]
        public async Task GetEvents_IncludesWarnings_SortedByCompetencyNameThenStart()
        {
            // To krav uten vakter: én advarsel per krav for hele åpningstiden, sortert på navn.
            using var seed = _fixture.CreateContext();
            var b = await SeedCompetency(seed, UniqueName("B"));
            var a = await SeedCompetency(seed, UniqueName("A"));
            var eventId = await SeedEventWithRequirements((b, 1), (a, 2));

            using var context = _fixture.CreateContext();
            var events = await new ResourceReader(context, Clock).GetEvents(new Actor(1, IsAdmin: false), At(17), At(18));
            var evt = events.Single(e => e.Id == eventId);

            Assert.Equal([a.Name, b.Name], evt.CompetencyWarnings.Select(w => w.CompetencyName));
            Assert.Equal(2, evt.CompetencyWarnings.First().MinimumRequired);
            Assert.All(evt.CompetencyWarnings, w => Assert.Equal(0, w.CurrentCount));
        }

        #endregion
    }
}
