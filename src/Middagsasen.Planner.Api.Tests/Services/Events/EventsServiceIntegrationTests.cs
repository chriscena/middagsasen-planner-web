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
    [Collection("Database")]
    public class EventsServiceIntegrationTests
    {
        private readonly DatabaseFixture _fixture;

        // Fast «nå» før testdataene (januar 2026), så ressursene ikke er avsluttet.
        private static readonly TimeProvider Clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

        public EventsServiceIntegrationTests(DatabaseFixture fixture)
        {
            _fixture = fixture;
        }

        private static ICurrentUserService MockCurrentUser(int userId, bool isAdmin = false)
        {
            var mock = Substitute.For<ICurrentUserService>();
            mock.UserId.Returns(userId);
            mock.IsAdmin.Returns(isAdmin);
            return mock;
        }

        private EventsService CreateService(PlannerDbContext context, int userId = 0, bool isAdmin = false)
        {
            var currentUser = MockCurrentUser(userId, isAdmin);
            return new EventsService(context, new ResourceReader(context, Clock), currentUser);
        }

        private static string UniqueName(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

        private async Task<User> SeedUser(PlannerDbContext context, string? firstName = null, bool isAdmin = false)
        {
            var user = new User
            {
                UserName = UniqueName("user"),
                FirstName = firstName ?? "Test",
                LastName = "User",
                Created = DateTime.UtcNow,
                IsAdmin = isAdmin,
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();
            return user;
        }

        private async Task<ResourceType> SeedResourceType(PlannerDbContext context, string? name = null)
        {
            var rt = new ResourceType
            {
                Name = name ?? UniqueName("RT"),
                DefaultStaff = 2,
            };
            context.ResourceTypes.Add(rt);
            await context.SaveChangesAsync();
            return rt;
        }

        private async Task<(Event evt, EventResource resource)> SeedEventWithResource(PlannerDbContext context)
        {
            var rt = await SeedResourceType(context);
            var evt = new Event
            {
                Name = UniqueName("Event"),
                StartTime = new DateTime(2026, 1, 15, 8, 0, 0),
                EndTime = new DateTime(2026, 1, 15, 16, 0, 0),
                Resources = new List<EventResource>
                {
                    new EventResource
                    {
                        ResourceTypeId = rt.ResourceTypeId,
                        StartTime = new DateTime(2026, 1, 15, 8, 0, 0),
                        EndTime = new DateTime(2026, 1, 15, 16, 0, 0),
                        MinimumStaff = 2,
                    }
                }
            };
            context.Events.Add(evt);
            await context.SaveChangesAsync();
            return (evt, evt.Resources.First());
        }

        #region Event CRUD

        [Fact]
        public async Task CreateEvent_ResourceWithId_CreatesNewResource_AndLeavesExistingUntouched()
        {
            // Arrange: klienten sender med id-en til en eksisterende ressurs (f.eks. ved kopiering).
            using var seedContext = _fixture.CreateContext();
            var (existingEvent, existingResource) = await SeedEventWithResource(seedContext);

            using var context = _fixture.CreateContext();
            var request = new EventRequest
            {
                Name = UniqueName("CreateWithId"),
                StartTime = new DateTime(2026, 2, 15, 8, 0, 0),
                EndTime = new DateTime(2026, 2, 15, 16, 0, 0),
                Resources = new List<ResourceRequest>
                {
                    new ResourceRequest
                    {
                        Id = existingResource.EventResourceId,
                        ResourceTypeId = existingResource.ResourceTypeId,
                        StartTime = new TimeOnly(9, 0),
                        EndTime = new TimeOnly(15, 0),
                        MinimumStaff = 1,
                    }
                }
            };

            // Act
            var result = await CreateService(context).CreateEvent(request);

            // Assert
            var created = Assert.Single(result.Resources);
            Assert.NotEqual(existingResource.EventResourceId, created.Id);
            Assert.Equal(1, created.MinimumStaff);

            using var verifyContext = _fixture.CreateContext();
            var untouched = await verifyContext.EventResource.AsNoTracking()
                .SingleAsync(r => r.EventResourceId == existingResource.EventResourceId);
            Assert.Equal(existingEvent.EventId, untouched.EventId);
            Assert.Equal(2, untouched.MinimumStaff);
        }

        [Fact]
        public async Task CreateEvent_SkipsDeletedResources()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var rt = await SeedResourceType(seedContext);

            using var context = _fixture.CreateContext();
            var request = new EventRequest
            {
                Name = UniqueName("SkipDeleted"),
                StartTime = new DateTime(2026, 1, 15, 8, 0, 0),
                EndTime = new DateTime(2026, 1, 15, 16, 0, 0),
                Resources = new List<ResourceRequest>
                {
                    new ResourceRequest { ResourceTypeId = rt.ResourceTypeId, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(15, 0), MinimumStaff = 1 },
                    new ResourceRequest { ResourceTypeId = rt.ResourceTypeId, StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(14, 0), MinimumStaff = 7, IsDeleted = true },
                },
            };

            // Act
            var result = await CreateService(context).CreateEvent(request);

            // Assert
            using var verifyContext = _fixture.CreateContext();
            var dbEvent = await verifyContext.Events.Include(e => e.Resources).AsNoTracking().SingleAsync(e => e.EventId == result.Id);
            Assert.Equal(1, Assert.Single(dbEvent.Resources).MinimumStaff);
        }

        [Fact]
        public async Task CreateEvent_PersistsToDatabase()
        {
            // Arrange
            var name = UniqueName("Create");
            using var seedContext = _fixture.CreateContext();
            var rt = await SeedResourceType(seedContext);

            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            var request = new EventRequest
            {
                Name = name,
                Description = "Test description",
                StartTime = new DateTime(2026, 1, 15, 8, 0, 0),
                EndTime = new DateTime(2026, 1, 15, 16, 0, 0),
                Resources = new List<ResourceRequest>
                {
                    new ResourceRequest
                    {
                        ResourceTypeId = rt.ResourceTypeId,
                        StartTime = new TimeOnly(9, 0),
                        EndTime = new TimeOnly(15, 0),
                        MinimumStaff = 3,
                    }
                }
            };

            // Act
            var result = await service.CreateEvent(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(name, result.Name);
            Assert.Equal("Test description", result.Description);

            // Verify in DB with fresh context
            using var verifyContext = _fixture.CreateContext();
            var dbEvent = await verifyContext.Events
                .Include(e => e.Resources)
                .AsNoTracking()
                .SingleOrDefaultAsync(e => e.EventId == result.Id);
            Assert.NotNull(dbEvent);
            Assert.Equal(name, dbEvent.Name);
            Assert.Single(dbEvent.Resources);
            Assert.Equal(3, dbEvent.Resources.First().MinimumStaff);
        }

        [Fact]
        public async Task GetEventById_ReturnsEventWithResources()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var (evt, resource) = await SeedEventWithResource(seedContext);

            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act
            var result = await service.GetEventById(evt.EventId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(evt.EventId, result.Id);
            Assert.Equal(evt.Name, result.Name);
            Assert.Single(result.Resources);
            Assert.Equal(resource.EventResourceId, result.Resources.First().Id);
            Assert.Equal(2, result.Resources.First().MinimumStaff);
        }

        [Fact]
        public async Task GetEvents_FiltersByDateRange()
        {
            // Arrange — seed events at different dates with unique names
            var janName = UniqueName("Jan");
            var febName = UniqueName("Feb");

            using var seedContext = _fixture.CreateContext();
            var rt = await SeedResourceType(seedContext);

            seedContext.Events.Add(new Event
            {
                Name = janName,
                StartTime = new DateTime(2027, 1, 10, 8, 0, 0),
                EndTime = new DateTime(2027, 1, 10, 16, 0, 0),
                Resources = new List<EventResource>
                {
                    new EventResource { ResourceTypeId = rt.ResourceTypeId, StartTime = new DateTime(2027, 1, 10, 8, 0, 0), EndTime = new DateTime(2027, 1, 10, 16, 0, 0), MinimumStaff = 1 }
                }
            });
            seedContext.Events.Add(new Event
            {
                Name = febName,
                StartTime = new DateTime(2027, 2, 10, 8, 0, 0),
                EndTime = new DateTime(2027, 2, 10, 16, 0, 0),
                Resources = new List<EventResource>
                {
                    new EventResource { ResourceTypeId = rt.ResourceTypeId, StartTime = new DateTime(2027, 2, 10, 8, 0, 0), EndTime = new DateTime(2027, 2, 10, 16, 0, 0), MinimumStaff = 1 }
                }
            });
            await seedContext.SaveChangesAsync();

            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act — query only January 2027
            var results = (await service.GetEvents(new DateTime(2027, 1, 1), new DateTime(2027, 2, 1))).ToList();

            // Assert
            Assert.Contains(results, r => r != null && r.Name == janName);
            Assert.DoesNotContain(results, r => r != null && r.Name == febName);
        }

        [Fact]
        public async Task UpdateEvent_UpdatesNameAndDescription()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var (evt, resource) = await SeedEventWithResource(seedContext);

            var updatedName = UniqueName("Updated");
            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            var request = new EventRequest
            {
                Name = updatedName,
                Description = "Updated description",
                StartTime = new DateTime(2026, 1, 15, 8, 0, 0),
                EndTime = new DateTime(2026, 1, 15, 16, 0, 0),
                Resources = new List<ResourceRequest>
                {
                    new ResourceRequest
                    {
                        Id = resource.EventResourceId,
                        ResourceTypeId = resource.ResourceTypeId,
                        StartTime = new TimeOnly(8, 0),
                        EndTime = new TimeOnly(16, 0),
                        MinimumStaff = resource.MinimumStaff,
                    }
                }
            };

            // Act
            var result = await service.UpdateEvent(evt.EventId, request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(updatedName, result.Name);
            Assert.Equal("Updated description", result.Description);

            // Verify in DB
            using var verifyContext = _fixture.CreateContext();
            var dbEvent = await verifyContext.Events
                .AsNoTracking()
                .SingleAsync(e => e.EventId == evt.EventId);
            Assert.Equal(updatedName, dbEvent.Name);
            Assert.Equal("Updated description", dbEvent.Description);
        }

        [Fact]
        public async Task UpdateEvent_CanAddAndRemoveResources()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var (evt, existingResource) = await SeedEventWithResource(seedContext);
            var newRt = await SeedResourceType(seedContext, UniqueName("NewRT"));

            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            var request = new EventRequest
            {
                Name = evt.Name,
                StartTime = new DateTime(2026, 1, 15, 8, 0, 0),
                EndTime = new DateTime(2026, 1, 15, 16, 0, 0),
                Resources = new List<ResourceRequest>
                {
                    // Delete existing resource
                    new ResourceRequest
                    {
                        Id = existingResource.EventResourceId,
                        ResourceTypeId = existingResource.ResourceTypeId,
                        StartTime = new TimeOnly(8, 0),
                        EndTime = new TimeOnly(16, 0),
                        MinimumStaff = existingResource.MinimumStaff,
                        IsDeleted = true,
                    },
                    // Add new resource
                    new ResourceRequest
                    {
                        ResourceTypeId = newRt.ResourceTypeId,
                        StartTime = new TimeOnly(10, 0),
                        EndTime = new TimeOnly(14, 0),
                        MinimumStaff = 4,
                    }
                }
            };

            // Act
            var result = await service.UpdateEvent(evt.EventId, request);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result.Resources);
            var newResource = result.Resources.First();
            Assert.Equal(newRt.ResourceTypeId, newResource.ResourceType.Id);
            Assert.Equal(4, newResource.MinimumStaff);

            // Verify old resource is gone
            using var verifyContext = _fixture.CreateContext();
            var oldResource = await verifyContext.EventResource
                .AsNoTracking()
                .SingleOrDefaultAsync(r => r.EventResourceId == existingResource.EventResourceId);
            Assert.Null(oldResource);
        }

        [Fact]
        public async Task DeleteEvent_RemovesFromDatabase()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var (evt, resource) = await SeedEventWithResource(seedContext);

            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act
            var result = await service.DeleteEvent(evt.EventId);

            // Assert: svaret er det slettede arrangementet med ressursene.
            Assert.Equal(evt.EventId, result.Id);
            Assert.Equal(resource.EventResourceId, Assert.Single(result.Resources).Id);

            using var verifyContext = _fixture.CreateContext();
            var dbEvent = await verifyContext.Events
                .AsNoTracking()
                .SingleOrDefaultAsync(e => e.EventId == evt.EventId);
            Assert.Null(dbEvent);
        }

        [Fact]
        public async Task DeleteEvent_ThrowsEntityNotFound_WhenNotFound()
        {
            // Arrange
            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act & Assert
            await Assert.ThrowsAsync<EntityNotFoundException>(() => service.DeleteEvent(999999));
        }

        #endregion

        #region Bemanning ved lagring (#151)

        // Ressursen fra SeedEventWithResource er 15.01.2026 08:00–16:00. Testene setter selv verdien skjemaet ble
        // lastet med (OriginalMinimumStaff), og hva «en annen admin» har endret i mellomtiden (SetMinimumStaff).

        private async Task SetMinimumStaff(int resourceId, int minimumStaff)
        {
            using var context = _fixture.CreateContext();
            await context.EventResource
                .Where(r => r.EventResourceId == resourceId)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.MinimumStaff, minimumStaff));
        }

        private async Task SeedShifts(PlannerDbContext context, EventResource resource, int count)
        {
            for (var i = 0; i < count; i++)
            {
                var user = await SeedUser(context);
                context.Shifts.Add(new EventResourceUser
                {
                    EventResourceId = resource.EventResourceId,
                    UserId = user.UserId,
                    StartTime = resource.StartTime,
                    EndTime = resource.EndTime,
                });
            }
            await context.SaveChangesAsync();
        }

        private async Task<(Event Event, EventResource Resource)> GetStored(int eventId, int resourceId)
        {
            using var verify = _fixture.CreateContext();
            var evt = await verify.Events.AsNoTracking().SingleAsync(e => e.EventId == eventId);
            var resource = await verify.EventResource.AsNoTracking().SingleAsync(r => r.EventResourceId == resourceId);
            return (evt, resource);
        }

        private static EventRequest StaffingRequest(Event evt, EventResource resource, int minimumStaff, int? originalMinimumStaff, string? name = null)
            => new()
            {
                Name = name ?? evt.Name,
                StartTime = evt.StartTime,
                EndTime = evt.EndTime,
                Resources =
                [
                    new ResourceRequest
                    {
                        Id = resource.EventResourceId,
                        ResourceTypeId = resource.ResourceTypeId,
                        StartTime = TimeOnly.FromDateTime(resource.StartTime),
                        EndTime = TimeOnly.FromDateTime(resource.EndTime),
                        MinimumStaff = minimumStaff,
                        OriginalMinimumStaff = originalMinimumStaff,
                    },
                ],
            };

        [Fact]
        public async Task UpdateEvent_UnchangedInForm_KeepsEmptySlotsAddedByOthers()
        {
            using var seed = _fixture.CreateContext();
            var (evt, resource) = await SeedEventWithResource(seed);
            // Skjemaet er lastet med 3. En annen admin har siden lagt til to ledige plasser.
            await SetMinimumStaff(resource.EventResourceId, 5);

            var newName = UniqueName("Renamed");
            using var context = _fixture.CreateContext();
            var result = await CreateService(context, isAdmin: true)
                .UpdateEvent(evt.EventId, StaffingRequest(evt, resource, minimumStaff: 3, originalMinimumStaff: 3, name: newName));

            Assert.Equal(5, Assert.Single(result.Resources).MinimumStaff);
            var (storedEvent, storedResource) = await GetStored(evt.EventId, resource.EventResourceId);
            Assert.Equal(5, storedResource.MinimumStaff);
            Assert.Equal(newName, storedEvent.Name);
        }

        [Fact]
        public async Task UpdateEvent_ChangedWithoutCompetition_SetsValue()
        {
            using var seed = _fixture.CreateContext();
            var (evt, resource) = await SeedEventWithResource(seed);
            await SetMinimumStaff(resource.EventResourceId, 3);

            using var context = _fixture.CreateContext();
            await CreateService(context, isAdmin: true)
                .UpdateEvent(evt.EventId, StaffingRequest(evt, resource, minimumStaff: 6, originalMinimumStaff: 3));

            Assert.Equal(6, (await GetStored(evt.EventId, resource.EventResourceId)).Resource.MinimumStaff);
        }

        [Fact]
        public async Task UpdateEvent_RetryWithSameForm_GivesNoErrorAndSameValue()
        {
            using var seed = _fixture.CreateContext();
            var (evt, resource) = await SeedEventWithResource(seed);
            await SetMinimumStaff(resource.EventResourceId, 3);
            var request = StaffingRequest(evt, resource, minimumStaff: 4, originalMinimumStaff: 3);

            using (var context = _fixture.CreateContext())
                await CreateService(context, isAdmin: true).UpdateEvent(evt.EventId, request);
            using (var context = _fixture.CreateContext())
                await CreateService(context, isAdmin: true).UpdateEvent(evt.EventId, request);

            Assert.Equal(4, (await GetStored(evt.EventId, resource.EventResourceId)).Resource.MinimumStaff);
        }

        [Fact]
        public async Task UpdateEvent_ChangedByOthers_ThrowsConflictWithStoredValues_AndSavesNothing()
        {
            using var seed = _fixture.CreateContext();
            var otherType = await SeedResourceType(seed, UniqueName("Barneheis"));
            var (evt, resource) = await SeedEventWithResource(seed);
            var storedTypeName = await seed.ResourceTypes.Where(t => t.ResourceTypeId == resource.ResourceTypeId).Select(t => t.Name).SingleAsync();
            // Skjemaet er lastet med 3 og settes til 4. En annen admin har siden lagt til to ledige plasser (5).
            await SetMinimumStaff(resource.EventResourceId, 5);

            // Skjemaet endrer også navn, start, vakttype og tider på ressursen, og legger til en ressurs.
            var request = StaffingRequest(evt, resource, minimumStaff: 4, originalMinimumStaff: 3, name: UniqueName("Renamed"));
            request.StartTime = evt.StartTime.AddHours(1);
            var requested = request.Resources.Single();
            requested.ResourceTypeId = otherType.ResourceTypeId;
            requested.StartTime = new TimeOnly(10, 0);
            request.Resources = request.Resources.Append(new ResourceRequest
            {
                ResourceTypeId = otherType.ResourceTypeId,
                StartTime = new TimeOnly(10, 0),
                EndTime = new TimeOnly(12, 0),
                MinimumStaff = 1,
            }).ToList();

            using var context = _fixture.CreateContext();
            var ex = await Assert.ThrowsAsync<ConcurrentUpdateException>(() =>
                CreateService(context, isAdmin: true).UpdateEvent(evt.EventId, request));

            // Meldingen viser lagret vakttype, lagrede tider og lagret verdi, ikke det skjemaet sendte.
            Assert.Equal(
                EventsService.StaffingChangedMessage(storedTypeName, resource.StartTime, resource.EndTime, 5),
                ex.Message);
            Assert.Contains($"{storedTypeName} 08:00–16:00", ex.Message);
            Assert.Contains("nå 5", ex.Message);

            // Ingenting er lagret: verken navn, tider, vakttype, bemanning eller den nye ressursen.
            var (storedEvent, storedResource) = await GetStored(evt.EventId, resource.EventResourceId);
            Assert.Equal(evt.Name, storedEvent.Name);
            Assert.Equal(evt.StartTime, storedEvent.StartTime);
            Assert.Equal(resource.ResourceTypeId, storedResource.ResourceTypeId);
            Assert.Equal(resource.StartTime, storedResource.StartTime);
            Assert.Equal(5, storedResource.MinimumStaff);
            using var verify = _fixture.CreateContext();
            Assert.Equal(1, await verify.EventResource.CountAsync(r => r.EventId == evt.EventId));
        }

        [Fact]
        public async Task UpdateEvent_DecreaseBelowShiftCountWithoutCompetition_IsAllowed()
        {
            using var seed = _fixture.CreateContext();
            var (evt, resource) = await SeedEventWithResource(seed);
            await SetMinimumStaff(resource.EventResourceId, 3);
            await SeedShifts(seed, resource, 2);

            using var context = _fixture.CreateContext();
            await CreateService(context, isAdmin: true)
                .UpdateEvent(evt.EventId, StaffingRequest(evt, resource, minimumStaff: 1, originalMinimumStaff: 3));

            Assert.Equal(1, (await GetStored(evt.EventId, resource.EventResourceId)).Resource.MinimumStaff);
        }

        [Fact]
        public async Task UpdateEvent_WithoutOriginalMinimumStaff_SetsAbsoluteValue()
        {
            using var seed = _fixture.CreateContext();
            var (evt, resource) = await SeedEventWithResource(seed);
            await SetMinimumStaff(resource.EventResourceId, 5);

            using var context = _fixture.CreateContext();
            await CreateService(context, isAdmin: true)
                .UpdateEvent(evt.EventId, StaffingRequest(evt, resource, minimumStaff: 3, originalMinimumStaff: null));

            Assert.Equal(3, (await GetStored(evt.EventId, resource.EventResourceId)).Resource.MinimumStaff);
        }

        [Fact]
        public async Task UpdateEvent_NewResource_UsesMinimumStaff_AndIgnoresOriginal()
        {
            using var seed = _fixture.CreateContext();
            var (evt, resource) = await SeedEventWithResource(seed);

            var request = StaffingRequest(evt, resource, minimumStaff: 2, originalMinimumStaff: 2);
            request.Resources = request.Resources.Append(new ResourceRequest
            {
                ResourceTypeId = resource.ResourceTypeId,
                StartTime = new TimeOnly(10, 0),
                EndTime = new TimeOnly(12, 0),
                MinimumStaff = 4,
                OriginalMinimumStaff = 1,
            }).ToList();

            using var context = _fixture.CreateContext();
            var result = await CreateService(context, isAdmin: true).UpdateEvent(evt.EventId, request);

            var created = Assert.Single(result.Resources, r => r.Id != resource.EventResourceId);
            Assert.Equal(4, created.MinimumStaff);
        }

        #endregion

        #region Messages

        [Fact]
        public async Task AddMessage_PersistsMessageToDatabase()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var user = await SeedUser(seedContext);
            var (evt, resource) = await SeedEventWithResource(seedContext);

            using var context = _fixture.CreateContext();
            var service = CreateService(context, userId: user.UserId);

            var request = new MessageRequest
            {
                Message = "Test message",
            };

            // Act
            var result = await service.AddMessage(resource.EventResourceId, user.UserId, request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Test message", result.Message);
            Assert.Equal(resource.EventResourceId, result.EventResourceId);

            // Verify in DB
            using var verifyContext = _fixture.CreateContext();
            var dbMessage = await verifyContext.Messages
                .AsNoTracking()
                .SingleOrDefaultAsync(m => m.EventResourceMessageId == result.Id);
            Assert.NotNull(dbMessage);
            Assert.Equal("Test message", dbMessage.Message);
        }

        [Fact]
        public async Task AddMessage_SetsCreatedByToProvidedUserId()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var user = await SeedUser(seedContext);
            var (evt, resource) = await SeedEventWithResource(seedContext);

            using var context = _fixture.CreateContext();
            // Innlogget bruker i servicen er bevisst en annen enn avsenderen som sendes inn.
            var service = CreateService(context, userId: 0);

            var request = new MessageRequest
            {
                Message = "CreatedBy test",
            };

            // Act
            var result = await service.AddMessage(resource.EventResourceId, user.UserId, request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(user.UserId, result.CreatedBy.Id);

            // Verify in DB
            using var verifyContext = _fixture.CreateContext();
            var dbMessage = await verifyContext.Messages
                .AsNoTracking()
                .SingleAsync(m => m.EventResourceMessageId == result.Id);
            Assert.Equal(user.UserId, dbMessage.CreatedBy);
        }

        [Fact]
        public async Task AddMessage_TrimsMessage()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var user = await SeedUser(seedContext);
            var (evt, resource) = await SeedEventWithResource(seedContext);

            using var context = _fixture.CreateContext();
            var service = CreateService(context, userId: user.UserId);

            var request = new MessageRequest
            {
                Message = "  Trimmet melding \n",
            };

            // Act
            var result = await service.AddMessage(resource.EventResourceId, user.UserId, request);

            // Assert
            Assert.Equal("Trimmet melding", result.Message);

            // Verify in DB
            using var verifyContext = _fixture.CreateContext();
            var dbMessage = await verifyContext.Messages
                .AsNoTracking()
                .SingleAsync(m => m.EventResourceMessageId == result.Id);
            Assert.Equal("Trimmet melding", dbMessage.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task AddMessage_ThrowsDomainValidation_WhenMessageIsBlank(string? text)
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var user = await SeedUser(seedContext);
            var (evt, resource) = await SeedEventWithResource(seedContext);

            using var context = _fixture.CreateContext();
            var service = CreateService(context, userId: user.UserId);

            // Act
            var ex = await Assert.ThrowsAsync<DomainValidationException>(
                () => service.AddMessage(resource.EventResourceId, user.UserId, new MessageRequest { Message = text! }));

            // Assert
            Assert.Equal(EventsService.MessageEmptyMessage, ex.Message);

            using var verifyContext = _fixture.CreateContext();
            Assert.False(await verifyContext.Messages.AnyAsync(m => m.EventResourceId == resource.EventResourceId));
        }

        [Fact]
        public async Task AddMessage_ThrowsDomainValidation_WhenMessageIsTooLong()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var user = await SeedUser(seedContext);
            var (evt, resource) = await SeedEventWithResource(seedContext);

            using var context = _fixture.CreateContext();
            var service = CreateService(context, userId: user.UserId);

            var request = new MessageRequest { Message = new string('a', MessageRequest.MaxLength + 1) };

            // Act
            var ex = await Assert.ThrowsAsync<DomainValidationException>(
                () => service.AddMessage(resource.EventResourceId, user.UserId, request));

            // Assert
            Assert.Equal(EventsService.MessageTooLongMessage, ex.Message);

            using var verifyContext = _fixture.CreateContext();
            Assert.False(await verifyContext.Messages.AnyAsync(m => m.EventResourceId == resource.EventResourceId));
        }

        [Fact]
        public async Task DeleteMessage_RemovesFromDatabase()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var user = await SeedUser(seedContext);
            var (evt, resource) = await SeedEventWithResource(seedContext);

            var message = new EventResourceMessage
            {
                EventResourceId = resource.EventResourceId,
                Message = "To be deleted",
                Created = DateTime.UtcNow,
                CreatedBy = user.UserId,
            };
            seedContext.Messages.Add(message);
            await seedContext.SaveChangesAsync();

            using var context = _fixture.CreateContext();
            // Forfatteren sletter sin egen melding.
            var service = CreateService(context, userId: user.UserId);

            // Act
            var result = await service.DeleteMessage(message.EventResourceMessageId, resource.EventResourceId);

            // Assert
            Assert.NotNull(result);

            using var verifyContext = _fixture.CreateContext();
            var dbMessage = await verifyContext.Messages
                .AsNoTracking()
                .SingleOrDefaultAsync(m => m.EventResourceMessageId == message.EventResourceMessageId);
            Assert.Null(dbMessage);
        }

        #endregion

        #region Ressurstider over midnatt

        [Fact]
        public async Task CreateEventFromTemplate_PlacesResourcesNearestEvent_OverMidnight()
        {
            // Arrange — nattmal 22:00–06:00 med vakter 21:30–06:30 og 01:00–03:00
            using var seedContext = _fixture.CreateContext();
            var rt = await SeedResourceType(seedContext);
            var template = new EventTemplate
            {
                Name = UniqueName("NightTemplate"),
                EventName = UniqueName("Night"),
                StartTime = new DateTime(2000, 1, 1, 22, 0, 0),
                EndTime = new DateTime(2000, 1, 1, 6, 0, 0),
                ResourceTemplates = new List<ResourceTemplate>
                {
                    new ResourceTemplate { ResourceTypeId = rt.ResourceTypeId, StartTime = new DateTime(2000, 1, 1, 21, 30, 0), EndTime = new DateTime(2000, 1, 1, 6, 30, 0), MinimumStaff = 1 },
                    new ResourceTemplate { ResourceTypeId = rt.ResourceTypeId, StartTime = new DateTime(2000, 1, 1, 1, 0, 0), EndTime = new DateTime(2000, 1, 1, 3, 0, 0), MinimumStaff = 2 },
                }
            };
            seedContext.EventTemplates.Add(template);
            await seedContext.SaveChangesAsync();

            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act — nyttårsaften
            var result = await service.CreateEventFromTemplate(template.EventTemplateId, new EventFromTemplateRequest { StartDate = new DateOnly(2026, 12, 31) });

            // Assert
            using var verifyContext = _fixture.CreateContext();
            var dbEvent = await verifyContext.Events
                .Include(e => e.Resources)
                .AsNoTracking()
                .SingleAsync(e => e.EventId == result.Id);
            Assert.Equal(new DateTime(2026, 12, 31, 22, 0, 0), dbEvent.StartTime);
            Assert.Equal(new DateTime(2027, 1, 1, 6, 0, 0), dbEvent.EndTime);

            var night = dbEvent.Resources.Single(r => r.MinimumStaff == 1);
            Assert.Equal(new DateTime(2026, 12, 31, 21, 30, 0), night.StartTime);
            Assert.Equal(new DateTime(2027, 1, 1, 6, 30, 0), night.EndTime);

            var afterMidnight = dbEvent.Resources.Single(r => r.MinimumStaff == 2);
            Assert.Equal(new DateTime(2027, 1, 1, 1, 0, 0), afterMidnight.StartTime);
            Assert.Equal(new DateTime(2027, 1, 1, 3, 0, 0), afterMidnight.EndTime);
        }

        [Fact]
        public async Task CreateEvent_PlacesResourcesNearestEvent_IgnoringSubmittedDate()
        {
            // Arrange — vaktliste 22:00–06:00 der alle tider sendes med vaktlistedatoen
            using var seedContext = _fixture.CreateContext();
            var rt = await SeedResourceType(seedContext);

            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            var request = new EventRequest
            {
                Name = UniqueName("Night"),
                StartTime = new DateTime(2026, 1, 15, 22, 0, 0),
                EndTime = new DateTime(2026, 1, 15, 6, 0, 0),
                Resources = new List<ResourceRequest>
                {
                    new ResourceRequest { ResourceTypeId = rt.ResourceTypeId, StartTime = new TimeOnly(1, 0), EndTime = new TimeOnly(3, 0), MinimumStaff = 1 },
                }
            };

            // Act
            var result = await service.CreateEvent(request);

            // Assert
            using var verifyContext = _fixture.CreateContext();
            var dbEvent = await verifyContext.Events
                .Include(e => e.Resources)
                .AsNoTracking()
                .SingleAsync(e => e.EventId == result.Id);
            Assert.Equal(new DateTime(2026, 1, 16, 6, 0, 0), dbEvent.EndTime);
            var resource = Assert.Single(dbEvent.Resources);
            Assert.Equal(new DateTime(2026, 1, 16, 1, 0, 0), resource.StartTime);
            Assert.Equal(new DateTime(2026, 1, 16, 3, 0, 0), resource.EndTime);
        }

        [Fact]
        public async Task UpdateEvent_PlacesExistingResourceNearestEvent_OverMidnight()
        {
            // Arrange — eksisterende vaktliste 08:00–16:00 flyttes til natt
            using var seedContext = _fixture.CreateContext();
            var (evt, resource) = await SeedEventWithResource(seedContext);

            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            var request = new EventRequest
            {
                Name = evt.Name,
                StartTime = new DateTime(2026, 1, 15, 22, 0, 0),
                EndTime = new DateTime(2026, 1, 16, 6, 0, 0),
                Resources = new List<ResourceRequest>
                {
                    new ResourceRequest { Id = resource.EventResourceId, ResourceTypeId = resource.ResourceTypeId, StartTime = new TimeOnly(23, 45), EndTime = new TimeOnly(2, 0), MinimumStaff = 2 },
                }
            };

            // Act
            await service.UpdateEvent(evt.EventId, request);

            // Assert
            using var verifyContext = _fixture.CreateContext();
            var dbResource = await verifyContext.EventResource
                .AsNoTracking()
                .SingleAsync(r => r.EventResourceId == resource.EventResourceId);
            Assert.Equal(new DateTime(2026, 1, 15, 23, 45, 0), dbResource.StartTime);
            Assert.Equal(new DateTime(2026, 1, 16, 2, 0, 0), dbResource.EndTime);
        }

        #endregion

        #region NotFound

        [Fact]
        public async Task GetEventById_ThrowsEntityNotFound_WhenNotFound()
        {
            // Arrange
            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act & Assert
            await Assert.ThrowsAsync<EntityNotFoundException>(() => service.GetEventById(999999));
        }

        [Fact]
        public async Task UpdateEvent_ThrowsEntityNotFound_WhenNotFound()
        {
            // Arrange
            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act & Assert
            await Assert.ThrowsAsync<EntityNotFoundException>(() => service.UpdateEvent(999999, new EventRequest { Name = "X", StartTime = new DateTime(2026, 1, 15, 8, 0, 0), EndTime = new DateTime(2026, 1, 15, 16, 0, 0), Resources = new List<ResourceRequest>() }));
        }

        #region Lagrede tider

        [Fact]
        public async Task CreateEvent_StoresSubmittedTimes()
        {
            // Arrange: arrangementet over midnatt; vakttidene er bare klokkeslett og plasseres nærmest arrangementet.
            using var seedContext = _fixture.CreateContext();
            var rt = await SeedResourceType(seedContext);

            using var context = _fixture.CreateContext();
            var request = new EventRequest
            {
                Name = UniqueName("SubmittedTimes"),
                StartTime = new DateTime(2026, 1, 15, 22, 0, 0),
                EndTime = new DateTime(2026, 1, 16, 2, 0, 0),
                Resources = new List<ResourceRequest>
                {
                    new ResourceRequest { ResourceTypeId = rt.ResourceTypeId, StartTime = new TimeOnly(23, 0), EndTime = new TimeOnly(1, 30), MinimumStaff = 1 },
                },
            };

            // Act
            var result = await CreateService(context).CreateEvent(request);

            // Assert
            using var verifyContext = _fixture.CreateContext();
            var dbEvent = await verifyContext.Events.Include(e => e.Resources).AsNoTracking().SingleAsync(e => e.EventId == result.Id);
            Assert.Equal(new DateTime(2026, 1, 15, 22, 0, 0), dbEvent.StartTime);
            Assert.Equal(new DateTime(2026, 1, 16, 2, 0, 0), dbEvent.EndTime);
            var dbResource = Assert.Single(dbEvent.Resources);
            Assert.Equal(new DateTime(2026, 1, 15, 23, 0, 0), dbResource.StartTime);
            Assert.Equal(new DateTime(2026, 1, 16, 1, 30, 0), dbResource.EndTime);
        }

        [Fact]
        public async Task UpdateEvent_StoresSubmittedTimes()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var (evt, resource) = await SeedEventWithResource(seedContext);

            using var context = _fixture.CreateContext();
            var request = new EventRequest
            {
                Name = evt.Name,
                StartTime = new DateTime(2026, 1, 17, 9, 15, 0),
                EndTime = new DateTime(2026, 1, 17, 17, 45, 0),
                Resources = new List<ResourceRequest>
                {
                    new ResourceRequest { Id = resource.EventResourceId, ResourceTypeId = resource.ResourceTypeId, StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(12, 30), MinimumStaff = 2 },
                },
            };

            // Act
            await CreateService(context).UpdateEvent(evt.EventId, request);

            // Assert
            using var verifyContext = _fixture.CreateContext();
            var dbEvent = await verifyContext.Events.Include(e => e.Resources).AsNoTracking().SingleAsync(e => e.EventId == evt.EventId);
            Assert.Equal(new DateTime(2026, 1, 17, 9, 15, 0), dbEvent.StartTime);
            Assert.Equal(new DateTime(2026, 1, 17, 17, 45, 0), dbEvent.EndTime);
            var dbResource = Assert.Single(dbEvent.Resources);
            Assert.Equal(new DateTime(2026, 1, 17, 10, 0, 0), dbResource.StartTime);
            Assert.Equal(new DateTime(2026, 1, 17, 12, 30, 0), dbResource.EndTime);
        }

        #endregion

        [Fact]
        public async Task CreateEventFromTemplate_ThrowsEntityNotFound_WhenNotFound()
        {
            // Arrange
            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act & Assert
            await Assert.ThrowsAsync<EntityNotFoundException>(() => service.CreateEventFromTemplate(999999, new EventFromTemplateRequest { StartDate = new DateOnly(2026, 1, 15) }));
        }

        [Fact]
        public async Task DeleteMessage_ThrowsEntityNotFound_WhenNotFound()
        {
            // Arrange
            using var context = _fixture.CreateContext();
            var service = CreateService(context);

            // Act & Assert
            await Assert.ThrowsAsync<EntityNotFoundException>(() => service.DeleteMessage(999999, 999999));
        }

        [Fact]
        public async Task AddMessage_ThrowsEntityNotFound_WhenEventResourceDoesNotExist()
        {
            // Arrange
            using var seedContext = _fixture.CreateContext();
            var user = await SeedUser(seedContext);

            using var context = _fixture.CreateContext();
            var service = CreateService(context, userId: user.UserId);

            // Act & Assert
            await Assert.ThrowsAsync<EntityNotFoundException>(
                () => service.AddMessage(999999, user.UserId, new MessageRequest { Message = "Hei" }));
        }

        #endregion
        #region Tilgang og flagg

        private async Task<EventResourceUser> SeedShift(PlannerDbContext context, int eventResourceId, int userId, string comment = "Original")
        {
            var shift = new EventResourceUser
            {
                EventResourceId = eventResourceId,
                UserId = userId,
                StartTime = new DateTime(2026, 1, 15, 9, 0, 0),
                EndTime = new DateTime(2026, 1, 15, 15, 0, 0),
                Comment = comment,
            };
            context.Shifts.Add(shift);
            await context.SaveChangesAsync();
            return shift;
        }

        private static async Task SeedTrainer(PlannerDbContext context, int resourceTypeId, int userId)
        {
            context.ResourceTypeTrainers.Add(new ResourceTypeTrainer { ResourceTypeId = resourceTypeId, UserId = userId });
            await context.SaveChangesAsync();
        }

        [Fact]
        public async Task GetEventById_ReturnsFlagsForOwner()
        {
            using var seedContext = _fixture.CreateContext();
            var owner = await SeedUser(seedContext, "Owner");
            var (evt, resource) = await SeedEventWithResource(seedContext);
            var shift = await SeedShift(seedContext, resource.EventResourceId, owner.UserId);

            using var context = _fixture.CreateContext();
            var service = CreateService(context, userId: owner.UserId);

            var result = await service.GetEventById(evt.EventId);

            var r = Assert.Single(result.Resources);
            Assert.True(r.IsMissingStaff);
            Assert.False(r.IsFull);
            Assert.False(r.IsPast);
            Assert.False(r.CanSignUp); // allerede påmeldt
            Assert.False(r.MustAnswerTraining); // ressurstypen har ikke opplæring
            Assert.Empty(r.CompetencyWarnings);
            var s = Assert.Single(r.Shifts);
            Assert.Equal(shift.EventResourceUserId, s.Id);
            Assert.True(s.IsMine);
            Assert.True(s.CanEdit);
            Assert.True(s.CanWithdraw);
            Assert.False(s.CanConfirmTraining);
            Assert.False(s.NeedsTraining);
        }

        [Fact]
        public async Task GetEvents_ReturnsFlagsForTrainer()
        {
            using var seedContext = _fixture.CreateContext();
            var owner = await SeedUser(seedContext, "Owner");
            var trainer = await SeedUser(seedContext, "Trainer");
            var (evt, resource) = await SeedEventWithResource(seedContext);
            await SeedTrainer(seedContext, resource.ResourceTypeId, trainer.UserId);
            seedContext.ResourceTypeTrainings.Add(new ResourceTypeTraining { UserId = owner.UserId, ResourceTypeId = resource.ResourceTypeId, TrainingComplete = false });
            await seedContext.SaveChangesAsync();
            await SeedShift(seedContext, resource.EventResourceId, owner.UserId);

            using var context = _fixture.CreateContext();
            var service = CreateService(context, userId: trainer.UserId);

            var result = await service.GetEvents(evt.StartTime.AddMinutes(-1), evt.StartTime.AddMinutes(1));

            var r = Assert.Single(Assert.Single(result, e => e.Id == evt.EventId).Resources);
            Assert.True(r.CanSignUp);
            Assert.True(r.MustAnswerTraining); // treneren har ikke svart på opplæring selv
            var s = Assert.Single(r.Shifts);
            Assert.False(s.IsMine);
            Assert.False(s.CanEdit);
            Assert.False(s.CanWithdraw);
            Assert.True(s.NeedsTraining);
            Assert.True(s.CanConfirmTraining);
        }

        [Fact]
        public async Task DeleteMessage_AllowsAdmin_ToDeleteOthersMessage()
        {
            using var seedContext = _fixture.CreateContext();
            var author = await SeedUser(seedContext, "Author");
            var admin = await SeedUser(seedContext, "Admin", isAdmin: true);
            var (_, resource) = await SeedEventWithResource(seedContext);
            var message = new EventResourceMessage { EventResourceId = resource.EventResourceId, Message = "Admin sletter", Created = DateTime.UtcNow, CreatedBy = author.UserId };
            seedContext.Messages.Add(message);
            await seedContext.SaveChangesAsync();

            using var context = _fixture.CreateContext();
            var service = CreateService(context, userId: admin.UserId, isAdmin: true);

            await service.DeleteMessage(message.EventResourceMessageId, resource.EventResourceId);

            using var verifyContext = _fixture.CreateContext();
            Assert.False(await verifyContext.Messages.AnyAsync(m => m.EventResourceMessageId == message.EventResourceMessageId));
        }

        [Fact]
        public async Task DeleteMessage_ThrowsForbidden_WhenOtherUserDeletesMessage()
        {
            using var seedContext = _fixture.CreateContext();
            var author = await SeedUser(seedContext, "Author");
            var otherUser = await SeedUser(seedContext, "Other");
            var (_, resource) = await SeedEventWithResource(seedContext);
            var message = new EventResourceMessage { EventResourceId = resource.EventResourceId, Message = "Skal ikke slettes", Created = DateTime.UtcNow, CreatedBy = author.UserId };
            seedContext.Messages.Add(message);
            await seedContext.SaveChangesAsync();

            using var context = _fixture.CreateContext();
            var service = CreateService(context, userId: otherUser.UserId);

            await Assert.ThrowsAsync<ForbiddenAccessException>(
                () => service.DeleteMessage(message.EventResourceMessageId, resource.EventResourceId));

            using var verifyContext = _fixture.CreateContext();
            Assert.True(await verifyContext.Messages.AnyAsync(m => m.EventResourceMessageId == message.EventResourceMessageId));
        }

        #endregion

    }
}
