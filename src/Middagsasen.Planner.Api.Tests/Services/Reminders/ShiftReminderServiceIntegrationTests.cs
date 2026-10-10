using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services.Reminders;
using Middagsasen.Planner.Api.Services.SmsSender;
using Middagsasen.Planner.Api.Tests.Infrastructure;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Middagsasen.Planner.Api.Tests.Services.Reminders
{
    /// <summary>
    /// Integrasjonstester mot <see cref="IShiftReminderService"/> med ekte repository og falsk <see cref="ISmsSender"/>.
    /// Databasen deles mellom testene, så hver test bruker sin egen vaktdag (<see cref="Now"/>), slik at kandidatene
    /// fra én test ikke blir med i en annen.
    /// </summary>
    [Collection("Database")]
    public class ShiftReminderServiceIntegrationTests
    {
        /// <summary>
        /// 1. februar 2027 kl. 16:05 UTC = 17:05 norsk tid (CET, UTC+1). Vaktdagen er da 2. februar, en tirsdag.
        /// <paramref name="testDay"/> flytter alt tre dager per test (testene seeder vakter dagen før og etter
        /// vaktdagen), innenfor normaltid.
        /// </summary>
        private static DateTimeOffset Now(int testDay, int hour = 16, int minute = 5)
            => new DateTimeOffset(2027, 2, 1, hour, minute, 0, TimeSpan.Zero).AddDays(testDay * DaysPerTest);

        private static DateOnly ShiftDateFor(int testDay) => new DateOnly(2027, 2, 2).AddDays(testDay * DaysPerTest);

        private const int DaysPerTest = 3;

        private readonly DatabaseFixture _fixture;
        private readonly ISmsSender _smsSender;

        public ShiftReminderServiceIntegrationTests(DatabaseFixture fixture)
        {
            _fixture = fixture;
            _smsSender = Substitute.For<ISmsSender>();
            _smsSender.SendMessages(Arg.Any<IEnumerable<SmsMessage>>()).Returns(new SmsResult { Success = true });
        }

        private ShiftReminderService CreateService(PlannerDbContext context, DateTimeOffset now, ReminderOptions? options = null)
            => new(new ShiftReminderRepository(context), _smsSender, Options.Create(options ?? new ReminderOptions()),
                new FakeTimeProvider(now), NullLogger<ShiftReminderService>.Instance);

        private async Task<ReminderRunResult> Run(DateTimeOffset now, ReminderOptions? options = null)
        {
            using var context = _fixture.CreateContext();
            return await CreateService(context, now, options).SendDueReminders(CancellationToken.None);
        }

        private static string UniquePhoneNo() => Random.Shared.Next(40000000, 99999999).ToString();

        private static string UniqueName(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

        private static async Task<User> SeedUser(PlannerDbContext context, string firstName, bool shiftReminders = true, bool inactive = false)
        {
            var user = new User
            {
                UserName = UniquePhoneNo(),
                FirstName = firstName,
                LastName = "Bruker",
                Created = DateTime.UtcNow,
                ShiftReminders = shiftReminders,
                Inactive = inactive,
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();
            return user;
        }

        /// <summary>Oppgave på <paramref name="date"/> fra <paramref name="startHour"/> til <paramref name="endHour"/> (kan gå over midnatt).</summary>
        private static async Task<EventResource> SeedResource(PlannerDbContext context, DateOnly date, int startHour, int endHour, string resourceTypeName, string eventName = "Åpningstid")
        {
            var start = date.ToDateTime(new TimeOnly(startHour, 0));
            var end = endHour > startHour ? date.ToDateTime(new TimeOnly(endHour, 0)) : date.AddDays(1).ToDateTime(new TimeOnly(endHour, 0));
            var evt = new Event
            {
                Name = eventName,
                StartTime = start,
                EndTime = end,
                Resources =
                [
                    new EventResource
                    {
                        ResourceType = new ResourceType { Name = resourceTypeName, DefaultShiftCount = 1 },
                        StartTime = start,
                        EndTime = end,
                        ShiftCount = 1,
                    },
                ],
            };
            context.Events.Add(evt);
            await context.SaveChangesAsync();
            return evt.Resources.Single();
        }

        private static async Task SeedShift(PlannerDbContext context, EventResource resource, User user, DateTime? start, DateTime? end)
        {
            context.Shifts.Add(new EventResourceUser { EventResourceId = resource.EventResourceId, UserId = user.UserId, StartTime = start, EndTime = end });
            await context.SaveChangesAsync();
        }

        /// <summary>Vakt med oppgavens tider satt eksplisitt (slik appen lagrer ved påmelding).</summary>
        private static Task SeedShift(PlannerDbContext context, EventResource resource, User user)
            => SeedShift(context, resource, user, resource.StartTime, resource.EndTime);

        private async Task<List<ShiftReminder>> GetReminders(int userId)
        {
            using var context = _fixture.CreateContext();
            return await context.ShiftReminders.AsNoTracking().Where(r => r.UserId == userId).ToListAsync();
        }

        private List<SmsMessage> SentMessages()
            => _smsSender.ReceivedCalls().SelectMany(c => (IEnumerable<SmsMessage>)c.GetArguments()[0]!).ToList();

        private static long PhoneNo(User user) => long.Parse($"47{user.UserName}");

        [Fact]
        public async Task SendDueReminders_SendsOneSmsPerUser_WithAllShiftsTomorrow_AndLogsSuccess()
        {
            const int day = 0;
            var now = Now(day);
            var shiftDate = ShiftDateFor(day);
            using var seed = _fixture.CreateContext();
            var kari = await SeedUser(seed, "Kari");
            var ola = await SeedUser(seed, "Ola");
            var storheis = await SeedResource(seed, shiftDate, 18, 22, "storheis");
            var kiosk = await SeedResource(seed, shiftDate, 10, 14, "kiosk");
            await SeedShift(seed, storheis, kari);
            await SeedShift(seed, kiosk, kari);
            await SeedShift(seed, storheis, ola);

            var result = await Run(now);

            Assert.Equal(new ReminderRunResult(true, 2, 0), result);
            await _smsSender.ReceivedWithAnyArgs(1).SendMessages(default!);
            var messages = SentMessages();
            Assert.Equal(2, messages.Count);
            var toKari = Assert.Single(messages, m => m.ReceiverPhoneNo == PhoneNo(kari));
            Assert.Equal("Hei Kari! Kjapp påminnelse om vakt i morgen, tirsdag 02.02: 10–14 kiosk, 18–22 storheis.", toKari.Body);
            var toOla = Assert.Single(messages, m => m.ReceiverPhoneNo == PhoneNo(ola));
            Assert.Equal("Hei Ola! Kjapp påminnelse om vakt i morgen, tirsdag 02.02: 18–22 storheis.", toOla.Body);

            foreach (var user in new[] { kari, ola })
            {
                var reminder = Assert.Single(await GetReminders(user.UserId));
                Assert.Equal(shiftDate, reminder.ShiftDate);
                Assert.True(reminder.Success);
                Assert.Null(reminder.Info);
                Assert.Equal(now.UtcDateTime, reminder.SentTime);
            }
        }

        [Fact]
        public async Task SendDueReminders_IncludesEventName_WhenNotDefault()
        {
            const int day = 1;
            var shiftDate = ShiftDateFor(day);
            using var seed = _fixture.CreateContext();
            var user = await SeedUser(seed, "Kari");
            var resource = await SeedResource(seed, shiftDate, 18, 22, "storheis", eventName: "Diskokveld");
            await SeedShift(seed, resource, user);

            await Run(Now(day));

            var message = Assert.Single(SentMessages(), m => m.ReceiverPhoneNo == PhoneNo(user));
            Assert.EndsWith(": 18–22 storheis (Diskokveld).", message.Body);
        }

        [Fact]
        public async Task SendDueReminders_SendsNothing_ToUsersWithoutOptIn_Inactive_OrWithShiftOnAnotherDay()
        {
            const int day = 2;
            var shiftDate = ShiftDateFor(day);
            using var seed = _fixture.CreateContext();
            var notOptedIn = await SeedUser(seed, "Uten", shiftReminders: false);
            var inactive = await SeedUser(seed, "Inaktiv", inactive: true);
            var today = await SeedUser(seed, "IDag");
            var dayAfterTomorrow = await SeedUser(seed, "Overimorgen");
            var tomorrow = await SeedResource(seed, shiftDate, 18, 22, "storheis");
            await SeedShift(seed, tomorrow, notOptedIn);
            await SeedShift(seed, tomorrow, inactive);
            await SeedShift(seed, await SeedResource(seed, shiftDate.AddDays(-1), 18, 22, "storheis"), today);
            await SeedShift(seed, await SeedResource(seed, shiftDate.AddDays(1), 18, 22, "storheis"), dayAfterTomorrow);

            var result = await Run(Now(day));

            Assert.Equal(new ReminderRunResult(true, 0, 0), result);
            await _smsSender.DidNotReceiveWithAnyArgs().SendMessages(default!);
            foreach (var user in new[] { notOptedIn, inactive, today, dayAfterTomorrow })
                Assert.Empty(await GetReminders(user.UserId));
        }

        [Fact]
        public async Task SendDueReminders_ShiftWithoutTimes_UsesResourceTimes()
        {
            const int day = 3;
            var shiftDate = ShiftDateFor(day);
            using var seed = _fixture.CreateContext();
            var user = await SeedUser(seed, "Kari");
            var resource = await SeedResource(seed, shiftDate, 9, 15, "skiutleie");
            await SeedShift(seed, resource, user, start: null, end: null);

            var result = await Run(Now(day));

            Assert.Equal(new ReminderRunResult(true, 1, 0), result);
            var message = Assert.Single(SentMessages(), m => m.ReceiverPhoneNo == PhoneNo(user));
            Assert.EndsWith(": 09–15 skiutleie.", message.Body);
        }

        [Fact]
        public async Task SendDueReminders_ShiftTimesDecideTheDay_NotResourceTimes()
        {
            const int day = 4;
            var shiftDate = ShiftDateFor(day);
            using var seed = _fixture.CreateContext();
            var afterMidnight = await SeedUser(seed, "Natt");
            var nextNight = await SeedUser(seed, "NesteNatt");
            // Oppgave i dag 20–04, men vakta starter 00:30 i morgen: teller som i morgen.
            var tonight = await SeedResource(seed, shiftDate.AddDays(-1), 20, 4, "storheis");
            await SeedShift(seed, tonight, afterMidnight, shiftDate.ToDateTime(new TimeOnly(0, 30)), shiftDate.ToDateTime(new TimeOnly(4, 0)));
            // Oppgave i morgen 20–04, men vakta starter 00:30 i overmorgen: teller ikke som i morgen.
            var tomorrowNight = await SeedResource(seed, shiftDate, 20, 4, "storheis");
            await SeedShift(seed, tomorrowNight, nextNight, shiftDate.AddDays(1).ToDateTime(new TimeOnly(0, 30)), shiftDate.AddDays(1).ToDateTime(new TimeOnly(4, 0)));

            var result = await Run(Now(day));

            Assert.Equal(new ReminderRunResult(true, 1, 0), result);
            var message = Assert.Single(SentMessages());
            Assert.Equal(PhoneNo(afterMidnight), message.ReceiverPhoneNo);
            Assert.EndsWith(": 00:30–04 storheis.", message.Body);
            Assert.Empty(await GetReminders(nextNight.UserId));
        }

        [Fact]
        public async Task SendDueReminders_SecondRun_SendsNothing()
        {
            const int day = 5;
            var shiftDate = ShiftDateFor(day);
            using var seed = _fixture.CreateContext();
            var user = await SeedUser(seed, "Kari");
            await SeedShift(seed, await SeedResource(seed, shiftDate, 18, 22, "storheis"), user);

            var first = await Run(Now(day));
            var second = await Run(Now(day, hour: 17));

            Assert.Equal(new ReminderRunResult(true, 1, 0), first);
            Assert.Equal(new ReminderRunResult(true, 0, 0), second);
            await _smsSender.ReceivedWithAnyArgs(1).SendMessages(default!);
            Assert.Single(await GetReminders(user.UserId));
        }

        [Fact]
        public async Task SendDueReminders_FailedSend_IsLogged_AndResentOnNextRun()
        {
            const int day = 6;
            var shiftDate = ShiftDateFor(day);
            using var seed = _fixture.CreateContext();
            var user = await SeedUser(seed, "Kari");
            await SeedShift(seed, await SeedResource(seed, shiftDate, 18, 22, "storheis"), user);
            _smsSender.SendMessages(Arg.Any<IEnumerable<SmsMessage>>()).Returns(new SmsResult { Success = false, Info = "500" });

            var first = await Run(Now(day));

            Assert.Equal(new ReminderRunResult(true, 0, 1), first);
            var failed = Assert.Single(await GetReminders(user.UserId));
            Assert.False(failed.Success);
            Assert.Equal("500", failed.Info);
            Assert.Equal(Now(day).UtcDateTime, failed.SentTime);

            _smsSender.SendMessages(Arg.Any<IEnumerable<SmsMessage>>()).Returns(new SmsResult { Success = true });
            var retryAt = Now(day, hour: 16, minute: 10);
            var second = await Run(retryAt);

            Assert.Equal(new ReminderRunResult(true, 1, 0), second);
            await _smsSender.ReceivedWithAnyArgs(2).SendMessages(default!);
            var resent = Assert.Single(await GetReminders(user.UserId));
            Assert.Equal(failed.ShiftReminderId, resent.ShiftReminderId);
            Assert.True(resent.Success);
            Assert.Null(resent.Info);
            Assert.Equal(retryAt.UtcDateTime, resent.SentTime);
        }

        [Fact]
        public async Task SendDueReminders_MapsResultPerMessage_WhenSmsServiceReportsPerReceiver()
        {
            const int day = 7;
            var shiftDate = ShiftDateFor(day);
            using var seed = _fixture.CreateContext();
            var ok = await SeedUser(seed, "Ok");
            var rejected = await SeedUser(seed, "Avvist");
            var resource = await SeedResource(seed, shiftDate, 18, 22, "storheis");
            await SeedShift(seed, resource, ok);
            await SeedShift(seed, resource, rejected);
            _smsSender.SendMessages(Arg.Any<IEnumerable<SmsMessage>>()).Returns(new SmsResult
            {
                Success = true,
                Messages =
                [
                    new SmsMessageResult { ReceiverPhoneNo = PhoneNo(ok), Success = true, Info = "Ok" },
                    new SmsMessageResult { ReceiverPhoneNo = PhoneNo(rejected), Success = false, Info = "Ugyldig nummer" },
                ],
            });

            var result = await Run(Now(day));

            Assert.Equal(new ReminderRunResult(true, 1, 1), result);
            var okRow = Assert.Single(await GetReminders(ok.UserId));
            Assert.True(okRow.Success);
            Assert.Null(okRow.Info);
            var rejectedRow = Assert.Single(await GetReminders(rejected.UserId));
            Assert.False(rejectedRow.Success);
            Assert.Equal("Ugyldig nummer", rejectedRow.Info);
        }

        [Fact]
        public async Task SendDueReminders_SmsSenderThrows_LogsFailureForAll_AndDoesNotThrow()
        {
            const int day = 8;
            var shiftDate = ShiftDateFor(day);
            using var seed = _fixture.CreateContext();
            var user = await SeedUser(seed, "Kari");
            await SeedShift(seed, await SeedResource(seed, shiftDate, 18, 22, "storheis"), user);
            _smsSender.SendMessages(Arg.Any<IEnumerable<SmsMessage>>()).ThrowsAsync(new HttpRequestException("nede"));

            var result = await Run(Now(day));

            Assert.Equal(new ReminderRunResult(true, 0, 1), result);
            var row = Assert.Single(await GetReminders(user.UserId));
            Assert.False(row.Success);
            Assert.Equal("nede", row.Info);
        }

        [Theory]
        [InlineData(15, 59)] // 16:59 norsk tid: før SendTime
        [InlineData(21, 0)]  // 22:00 norsk tid: ved RetryUntil
        [InlineData(23, 30)] // 00:30 norsk tid neste dag
        public async Task SendDueReminders_OutsideSendWindow_DoesNotRun(int utcHour, int utcMinute)
        {
            const int day = 9;
            var shiftDate = ShiftDateFor(day);
            using var seed = _fixture.CreateContext();
            var user = await SeedUser(seed, "Kari");
            await SeedShift(seed, await SeedResource(seed, shiftDate, 18, 22, "storheis"), user);

            var result = await Run(Now(day, utcHour, utcMinute));

            Assert.Equal(new ReminderRunResult(false, 0, 0), result);
            await _smsSender.DidNotReceiveWithAnyArgs().SendMessages(default!);
            Assert.Empty(await GetReminders(user.UserId));
        }

        [Fact]
        public async Task SendDueReminders_Disabled_DoesNotRun()
        {
            const int day = 10;
            var shiftDate = ShiftDateFor(day);
            using var seed = _fixture.CreateContext();
            var user = await SeedUser(seed, "Kari");
            await SeedShift(seed, await SeedResource(seed, shiftDate, 18, 22, "storheis"), user);

            var result = await Run(Now(day), new ReminderOptions { Enabled = false });

            Assert.Equal(new ReminderRunResult(false, 0, 0), result);
            await _smsSender.DidNotReceiveWithAnyArgs().SendMessages(default!);
            Assert.Empty(await GetReminders(user.UserId));
        }

        [Fact]
        public async Task SendDueReminders_ConcurrentRunAlreadySent_ThrowsOnUniqueIndex_AndSendsNoSecondSms()
        {
            const int day = 11;
            var shiftDate = ShiftDateFor(day);
            using var seed = _fixture.CreateContext();
            var user = await SeedUser(seed, "Kari");
            await SeedShift(seed, await SeedResource(seed, shiftDate, 18, 22, "storheis"), user);

            // En annen instans rekker å sende og lagre mellom utvelgelsen og lagringen her: den unike indeksen avviser raden.
            PlannerDbContext context = null!;
            var interceptor = new BeforeSaveInterceptor(() => context.Database.ExecuteSqlInterpolatedAsync(
                $"insert into ShiftReminders (UserId, ShiftDate, SentTime, Success) values ({user.UserId}, {shiftDate}, {DateTime.UtcNow}, 1)"));
            context = _fixture.CreateContext(interceptor);
            using var _ = context;

            await Assert.ThrowsAsync<DbUpdateException>(() => CreateService(context, Now(day)).SendDueReminders(CancellationToken.None));

            var row = Assert.Single(await GetReminders(user.UserId));
            Assert.True(row.Success);
            // Neste kjøring finner ingen kandidat og sender ikke på nytt.
            Assert.Equal(new ReminderRunResult(true, 0, 0), await Run(Now(day, hour: 17)));
            await _smsSender.ReceivedWithAnyArgs(1).SendMessages(default!);
        }
    }
}
