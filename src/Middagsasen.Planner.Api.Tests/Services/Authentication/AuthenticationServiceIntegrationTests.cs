using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services.Authentication;
using Middagsasen.Planner.Api.Services.SmsSender;
using Middagsasen.Planner.Api.Services.Users;
using Middagsasen.Planner.Api.Tests.Infrastructure;
using NSubstitute;

namespace Middagsasen.Planner.Api.Tests.Services.Authentication
{
    [Collection("Database")]
    public class AuthenticationServiceIntegrationTests
    {
        private readonly DatabaseFixture _fixture;
        private readonly ISmsSender _smsSender = Substitute.For<ISmsSender>();

        public AuthenticationServiceIntegrationTests(DatabaseFixture fixture)
        {
            _fixture = fixture;
        }

        /// <summary>Fast tidspunkt (hele sekunder, så det lagres eksakt i databasen).</summary>
        private static readonly DateTimeOffset Now = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
        private static readonly AuthOptions DefaultOptions = new();

        private AuthenticationService CreateService(PlannerDbContext context, TimeProvider? timeProvider = null)
        {
            var time = timeProvider ?? TimeProvider.System;
            return new AuthenticationService(context, _smsSender, CreateSessionTokens(time), Options.Create(DefaultOptions), time);
        }

        private static SessionTokens CreateSessionTokens(TimeProvider timeProvider)
        {
            var authSettings = Substitute.For<IAuthSettings>();
            authSettings.Secret.Returns(new string('x', 64));
            return new SessionTokens(authSettings, Options.Create(DefaultOptions), timeProvider);
        }

        /// <summary>Logger inn med egen DbContext, slik hver forespørsel får i appen.</summary>
        private async Task<AuthResponse> Authenticate(string phoneNo, string password, TimeProvider? timeProvider = null)
        {
            using var context = _fixture.CreateContext();
            return await CreateService(context, timeProvider).Authenticate(new AuthRequest { UserName = phoneNo, Password = password });
        }

        private User StoredUser(string phoneNo) => Assert.Single(UsersWithUserName(phoneNo));

        private static string UniquePhoneNo() => Random.Shared.Next(40000000, 99999999).ToString();

        /// <summary>Formater brukere skriver nummeret inn i. Hver «x» erstattes med neste siffer i nummeret.</summary>
        public static TheoryData<string> PhoneNoFormats => new()
        {
            "xxxxxxxx",
            "+47 xxx xx xxx",
            "47xxxxxxxx",
            "0047xxxxxxxx",
        };

        private static string Format(string format, string phoneNo)
        {
            var digits = new Queue<char>(phoneNo);
            return string.Concat(format.Select(c => c == 'x' ? digits.Dequeue() : c));
        }

        private async Task<User> SeedUser(string userName, bool inactive = false, string? password = null, DateTime? otpCreated = null, int failedOtpAttempts = 0)
        {
            using var context = _fixture.CreateContext();
            var user = new User
            {
                UserName = userName,
                FirstName = "Eksisterende",
                LastName = "Bruker",
                Inactive = inactive,
                Created = DateTime.UtcNow,
                OneTimePassword = otpCreated.HasValue ? "1234" : null,
                OtpCreated = otpCreated,
                FailedOtpAttempts = failedOtpAttempts,
            };
            if (password != null)
            {
                user.Salt = PasswordHasher.CreateSalt();
                user.EncryptedPassword = PasswordHasher.HashPassword(password, user.Salt);
            }
            context.Users.Add(user);
            await context.SaveChangesAsync();
            return user;
        }

        private List<User> UsersWithUserName(string userName)
        {
            using var context = _fixture.CreateContext();
            return context.Users.AsNoTracking().Where(u => u.UserName == userName).ToList();
        }

        [Theory]
        [MemberData(nameof(PhoneNoFormats))]
        public async Task GenerateOtpForUser_ExistingUser_DoesNotCreateNewUser(string format)
        {
            var phoneNo = UniquePhoneNo();
            var existing = await SeedUser(phoneNo);

            using var context = _fixture.CreateContext();
            var result = await CreateService(context).GenerateOtpForUser(new OtpRequest { UserName = Format(format, phoneNo) });

            Assert.Equal(OtpStatus.Sent, result.Status);
            var stored = Assert.Single(UsersWithUserName(phoneNo));
            Assert.Equal(existing.UserId, stored.UserId);
            Assert.NotNull(stored.OneTimePassword);
            await _smsSender.Received(1).SendMessages(Arg.Is<IEnumerable<SmsMessage>>(
                m => m.Single().ReceiverPhoneNo == long.Parse($"47{phoneNo}")));
        }

        [Fact]
        public async Task GenerateOtpForUser_InactiveUser_DoesNotCreateNewUser()
        {
            var phoneNo = UniquePhoneNo();
            var inactive = await SeedUser(phoneNo, inactive: true);

            using var context = _fixture.CreateContext();
            await CreateService(context).GenerateOtpForUser(new OtpRequest { UserName = $"+47 {phoneNo}" });

            var stored = Assert.Single(UsersWithUserName(phoneNo));
            Assert.Equal(inactive.UserId, stored.UserId);
            Assert.True(stored.Inactive);
        }

        [Fact]
        public async Task GenerateOtpForUser_NewPhoneNo_CreatesOneUserWithNormalizedUserName()
        {
            var phoneNo = UniquePhoneNo();

            using var context = _fixture.CreateContext();
            var result = await CreateService(context).GenerateOtpForUser(
                new OtpRequest { UserName = $"+47 {phoneNo[..3]} {phoneNo[3..5]} {phoneNo[5..]}" });

            Assert.Equal(OtpStatus.Sent, result.Status);
            var stored = Assert.Single(UsersWithUserName(phoneNo));
            Assert.NotNull(stored.OneTimePassword);
            Assert.False(stored.Inactive);
        }

        [Fact]
        public async Task GenerateOtpForUser_SendsOtp_WhenAdminCreatesUserConcurrently()
        {
            var phoneNo = UniquePhoneNo();
            User? createdByAdmin = null;

            // En administrator oppretter brukeren mellom oppslaget og lagringen. Da er det ikke sendt noen kode.
            using var context = _fixture.CreateContext(new BeforeSaveInterceptor(async () => createdByAdmin = await SeedUser(phoneNo)));
            var result = await CreateService(context).GenerateOtpForUser(new OtpRequest { UserName = phoneNo });

            Assert.Equal(OtpStatus.Sent, result.Status);
            var stored = Assert.Single(UsersWithUserName(phoneNo));
            Assert.Equal(createdByAdmin!.UserId, stored.UserId);
            Assert.NotNull(stored.OneTimePassword);
            Assert.NotNull(stored.OtpCreated);
            await _smsSender.Received(1).SendMessages(Arg.Is<IEnumerable<SmsMessage>>(
                m => m.Single().ReceiverPhoneNo == long.Parse($"47{phoneNo}")
                    && m.Single().Body.Contains(stored.OneTimePassword!)));
        }

        [Fact]
        public async Task GenerateOtpForUser_ReturnsTooManyRequests_WhenOtherOtpRequestCreatesUserConcurrently()
        {
            var phoneNo = UniquePhoneNo();
            var otpCreated = DateTime.UtcNow;

            // En parallell OTP-forespørsel oppretter brukeren og sender kode mellom oppslaget og lagringen.
            using var context = _fixture.CreateContext(new BeforeSaveInterceptor(() => SeedUser(phoneNo, otpCreated: otpCreated)));
            var result = await CreateService(context).GenerateOtpForUser(new OtpRequest { UserName = phoneNo });

            Assert.Equal(OtpStatus.TooManyRequests, result.Status);
            var stored = Assert.Single(UsersWithUserName(phoneNo));
            Assert.Equal("1234", stored.OneTimePassword);
            await _smsSender.DidNotReceive().SendMessages(Arg.Any<IEnumerable<SmsMessage>>());
        }

        [Fact]
        public async Task GenerateOtpForUser_ReturnsInvalidPhoneNumber_ForInvalidInput()
        {
            using var context = _fixture.CreateContext();
            var result = await CreateService(context).GenerateOtpForUser(new OtpRequest { UserName = "1234" });

            Assert.Equal(OtpStatus.InvalidPhoneNumber, result.Status);
        }

        [Theory]
        [InlineData("46{0}")]
        [InlineData("+46 {0}")]
        [InlineData("0046{0}")]
        [InlineData("1{0}")]
        public async Task GenerateOtpForUser_RejectsForeignNumber_EvenWhenNorwegianUserWithSameDigitsExists(string format)
        {
            // Et utenlandsk nummer som slutter på de samme 8 sifrene må ikke treffe den norske brukeren,
            // ellers ville koden til brukeren 92345678 blitt sendt til +46 92345678.
            var phoneNo = UniquePhoneNo();
            await SeedUser(phoneNo);
            var foreignPhoneNo = string.Format(format, phoneNo);

            using var context = _fixture.CreateContext();
            var result = await CreateService(context).GenerateOtpForUser(new OtpRequest { UserName = foreignPhoneNo });

            Assert.Equal(OtpStatus.InvalidPhoneNumber, result.Status);
            var stored = Assert.Single(UsersWithUserName(phoneNo));
            Assert.Null(stored.OneTimePassword);
            Assert.Null(stored.OtpCreated);
            using var verifyContext = _fixture.CreateContext();
            Assert.False(verifyContext.Users.Any(u => u.UserName.EndsWith(phoneNo) && u.UserName != phoneNo));
            await _smsSender.DidNotReceive().SendMessages(Arg.Any<IEnumerable<SmsMessage>>());
        }

        [Fact]
        public async Task GenerateOtpForUser_SendsSmsToNumberDerivedFromNormalizedUserName()
        {
            var phoneNo = UniquePhoneNo();

            using var context = _fixture.CreateContext();
            var result = await CreateService(context).GenerateOtpForUser(new OtpRequest { UserName = $"0047 {phoneNo}" });

            Assert.Equal(OtpStatus.Sent, result.Status);
            await _smsSender.Received(1).SendMessages(Arg.Is<IEnumerable<SmsMessage>>(
                m => m.Single().ReceiverPhoneNo == long.Parse($"47{phoneNo}")));
        }

        [Fact]
        public async Task Database_RejectsTwoUsersWithSameUserName()
        {
            var phoneNo = UniquePhoneNo();
            await SeedUser(phoneNo, inactive: true);

            using var context = _fixture.CreateContext();
            context.Users.Add(new User { UserName = phoneNo, Created = DateTime.UtcNow });

            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }

        [Theory]
        [MemberData(nameof(PhoneNoFormats))]
        public async Task Authenticate_FindsUser_RegardlessOfInputFormat(string format)
        {
            var phoneNo = UniquePhoneNo();
            await SeedUser(phoneNo, password: "hemmelig");

            using var context = _fixture.CreateContext();
            var result = await CreateService(context).Authenticate(
                new AuthRequest { UserName = Format(format, phoneNo), Password = "hemmelig" });

            Assert.Equal(AuthStatus.Success, result.Status);
            Assert.False(string.IsNullOrEmpty(result.Token));
        }

        [Fact]
        public async Task Authenticate_Fails_ForInactiveUser()
        {
            var phoneNo = UniquePhoneNo();
            await SeedUser(phoneNo, inactive: true, password: "hemmelig");

            using var context = _fixture.CreateContext();
            var result = await CreateService(context).Authenticate(new AuthRequest { UserName = phoneNo, Password = "hemmelig" });

            Assert.Equal(AuthStatus.AuthenticationFailed, result.Status);
        }

        [Fact]
        public async Task GenerateOtpForUser_ReturnsTooManyRequests_WithinThrottleWindow()
        {
            var phoneNo = UniquePhoneNo();
            await SeedUser(phoneNo, otpCreated: Now.UtcDateTime);

            using var context = _fixture.CreateContext();
            var justBefore = new FakeTimeProvider(Now + DefaultOptions.OtpThrottle - TimeSpan.FromSeconds(1));
            var result = await CreateService(context, justBefore).GenerateOtpForUser(new OtpRequest { UserName = phoneNo });

            Assert.Equal(OtpStatus.TooManyRequests, result.Status);
            var stored = Assert.Single(UsersWithUserName(phoneNo));
            Assert.Equal("1234", stored.OneTimePassword);
            await _smsSender.DidNotReceive().SendMessages(Arg.Any<IEnumerable<SmsMessage>>());
        }

        [Fact]
        public async Task GenerateOtpForUser_SendsNewOtp_WhenThrottleWindowHasPassed()
        {
            var phoneNo = UniquePhoneNo();
            await SeedUser(phoneNo, otpCreated: Now.UtcDateTime);

            using var context = _fixture.CreateContext();
            var later = Now + DefaultOptions.OtpThrottle;
            var result = await CreateService(context, new FakeTimeProvider(later)).GenerateOtpForUser(new OtpRequest { UserName = phoneNo });

            Assert.Equal(OtpStatus.Sent, result.Status);
            var stored = Assert.Single(UsersWithUserName(phoneNo));
            Assert.Equal(later.UtcDateTime, stored.OtpCreated);
            await _smsSender.Received(1).SendMessages(Arg.Any<IEnumerable<SmsMessage>>());
        }

        [Fact]
        public async Task GenerateOtpForUser_NewUser_UsesTimeProviderAndFourDigitCode()
        {
            var phoneNo = UniquePhoneNo();

            using var context = _fixture.CreateContext();
            var result = await CreateService(context, new FakeTimeProvider(Now)).GenerateOtpForUser(new OtpRequest { UserName = phoneNo });

            Assert.Equal(OtpStatus.Sent, result.Status);
            var stored = Assert.Single(UsersWithUserName(phoneNo));
            Assert.Matches("^[0-9]{4}$", stored.OneTimePassword);
            Assert.Equal(Now.UtcDateTime, stored.OtpCreated);
            Assert.Equal(Now.UtcDateTime, stored.Created);
        }

        [Fact]
        public async Task Authenticate_AcceptsOtp_JustBeforeExpiry()
        {
            var phoneNo = UniquePhoneNo();
            await SeedUser(phoneNo, otpCreated: Now.UtcDateTime);

            using var context = _fixture.CreateContext();
            var justBefore = new FakeTimeProvider(Now + DefaultOptions.OtpLifetime - TimeSpan.FromSeconds(1));
            var result = await CreateService(context, justBefore).Authenticate(new AuthRequest { UserName = phoneNo, Password = "1234" });

            Assert.Equal(AuthStatus.Success, result.Status);
            var stored = Assert.Single(UsersWithUserName(phoneNo));
            Assert.Null(stored.OneTimePassword);
            Assert.Null(stored.OtpCreated);
        }

        [Fact]
        public async Task Authenticate_RejectsOtp_AfterExpiry()
        {
            var phoneNo = UniquePhoneNo();
            await SeedUser(phoneNo, otpCreated: Now.UtcDateTime);

            using var context = _fixture.CreateContext();
            var expired = new FakeTimeProvider(Now + DefaultOptions.OtpLifetime);
            var result = await CreateService(context, expired).Authenticate(new AuthRequest { UserName = phoneNo, Password = "1234" });

            Assert.Equal(AuthStatus.AuthenticationFailed, result.Status);
            var stored = Assert.Single(UsersWithUserName(phoneNo));
            Assert.Equal("1234", stored.OneTimePassword);
        }

        [Fact]
        public async Task Authenticate_ReturnsTokenForCreatedSession()
        {
            var phoneNo = UniquePhoneNo();
            await SeedUser(phoneNo, password: "hemmelig");

            using var context = _fixture.CreateContext();
            var result = await CreateService(context).Authenticate(new AuthRequest { UserName = phoneNo, Password = "hemmelig" });

            var sessionId = CreateSessionTokens(TimeProvider.System).ReadSessionId(result.Token!);
            Assert.NotNull(sessionId);
            using var verifyContext = _fixture.CreateContext();
            Assert.True(verifyContext.UserSessions.Any(s => s.UserSessionId == sessionId));
        }

        [Fact]
        public async Task Authenticate_SetsSessionCreated_FromTimeProvider()
        {
            var phoneNo = UniquePhoneNo();
            await SeedUser(phoneNo, password: "hemmelig");

            var result = await Authenticate(phoneNo, "hemmelig", new FakeTimeProvider(Now));

            var sessionId = CreateSessionTokens(new FakeTimeProvider(Now)).ReadSessionId(result.Token!);
            using var verifyContext = _fixture.CreateContext();
            Assert.Equal(Now.UtcDateTime, verifyContext.UserSessions.Single(s => s.UserSessionId == sessionId).Created);
        }

        [Fact]
        public async Task GetUserBySessionId_ReturnsActor_ForActiveUser()
        {
            var phoneNo = UniquePhoneNo();
            var user = await SeedUser(phoneNo, password: "hemmelig");
            var result = await Authenticate(phoneNo, "hemmelig");
            var sessionId = CreateSessionTokens(TimeProvider.System).ReadSessionId(result.Token!)!.Value;

            using var context = _fixture.CreateContext();
            var actor = await CreateService(context).GetUserBySessionId(sessionId);

            Assert.Equal(user.UserId, actor?.UserId);
        }

        [Fact]
        public async Task GetUserBySessionId_ReturnsNull_ForInactiveUser_EvenWhenSessionExists()
        {
            var phoneNo = UniquePhoneNo();
            var user = await SeedUser(phoneNo, password: "hemmelig");
            var result = await Authenticate(phoneNo, "hemmelig");
            var sessionId = CreateSessionTokens(TimeProvider.System).ReadSessionId(result.Token!)!.Value;
            using (var deactivateContext = _fixture.CreateContext())
            {
                deactivateContext.Users.Single(u => u.UserId == user.UserId).Inactive = true;
                await deactivateContext.SaveChangesAsync();
            }

            using var context = _fixture.CreateContext();
            Assert.Null(await CreateService(context).GetUserBySessionId(sessionId));
            using var verifyContext = _fixture.CreateContext();
            Assert.True(verifyContext.UserSessions.Any(s => s.UserSessionId == sessionId));
        }

        [Fact]
        public async Task Authenticate_WrongOtp_IncrementsAndStoresFailedAttempts()
        {
            var phoneNo = UniquePhoneNo();
            await SeedUser(phoneNo, otpCreated: Now.UtcDateTime);
            var time = new FakeTimeProvider(Now);

            Assert.Equal(AuthStatus.AuthenticationFailed, (await Authenticate(phoneNo, "0000", time)).Status);
            Assert.Equal(AuthStatus.AuthenticationFailed, (await Authenticate(phoneNo, "0001", time)).Status);

            var stored = StoredUser(phoneNo);
            Assert.Equal(2, stored.FailedOtpAttempts);
            Assert.Equal("1234", stored.OneTimePassword);
            Assert.Equal(Now.UtcDateTime, stored.OtpCreated);
        }

        [Fact]
        public async Task Authenticate_InvalidatesOtp_AfterMaxFailedAttempts()
        {
            var phoneNo = UniquePhoneNo();
            await SeedUser(phoneNo, otpCreated: Now.UtcDateTime);
            var time = new FakeTimeProvider(Now);

            for (var i = 0; i < DefaultOptions.MaxOtpAttempts; i++)
                Assert.Equal(AuthStatus.AuthenticationFailed, (await Authenticate(phoneNo, "0000", time)).Status);

            var result = await Authenticate(phoneNo, "1234", time);

            // Samme svar som for feil kode, så det ikke avsløres at koden er ugyldiggjort.
            Assert.Equal(AuthStatus.AuthenticationFailed, result.Status);
            Assert.Null(result.Token);
            var stored = StoredUser(phoneNo);
            Assert.Null(stored.OneTimePassword);
            Assert.Null(stored.OtpCreated);
            Assert.Equal(DefaultOptions.MaxOtpAttempts, stored.FailedOtpAttempts);
        }

        [Fact]
        public async Task Authenticate_AcceptsOtp_AfterFewerFailedAttemptsThanMax_AndResetsCounter()
        {
            var phoneNo = UniquePhoneNo();
            await SeedUser(phoneNo, otpCreated: Now.UtcDateTime);
            var time = new FakeTimeProvider(Now);

            for (var i = 0; i < DefaultOptions.MaxOtpAttempts - 1; i++)
                await Authenticate(phoneNo, "0000", time);

            var result = await Authenticate(phoneNo, "1234", time);

            Assert.Equal(AuthStatus.Success, result.Status);
            var stored = StoredUser(phoneNo);
            Assert.Equal(0, stored.FailedOtpAttempts);
            Assert.Null(stored.OneTimePassword);
            Assert.Null(stored.OtpCreated);
        }

        [Fact]
        public async Task Authenticate_RejectsOtp_WhenStoredAttemptsHaveReachedMax()
        {
            // F.eks. hvis MaxOtpAttempts er senket etter at forsøkene ble gjort.
            var phoneNo = UniquePhoneNo();
            await SeedUser(phoneNo, otpCreated: Now.UtcDateTime, failedOtpAttempts: DefaultOptions.MaxOtpAttempts);

            var result = await Authenticate(phoneNo, "1234", new FakeTimeProvider(Now));

            Assert.Equal(AuthStatus.AuthenticationFailed, result.Status);
        }

        [Fact]
        public async Task GenerateOtpForUser_ResetsFailedOtpAttempts()
        {
            var phoneNo = UniquePhoneNo();
            await SeedUser(phoneNo, otpCreated: Now.UtcDateTime, failedOtpAttempts: DefaultOptions.MaxOtpAttempts - 1);

            using var context = _fixture.CreateContext();
            var later = new FakeTimeProvider(Now + DefaultOptions.OtpThrottle);
            var result = await CreateService(context, later).GenerateOtpForUser(new OtpRequest { UserName = phoneNo });

            Assert.Equal(OtpStatus.Sent, result.Status);
            Assert.Equal(0, StoredUser(phoneNo).FailedOtpAttempts);
        }

        [Fact]
        public async Task Authenticate_NewOtp_CanBeUsed_AfterOldOtpWasInvalidated()
        {
            var phoneNo = UniquePhoneNo();
            await SeedUser(phoneNo, otpCreated: Now.UtcDateTime);
            var time = new FakeTimeProvider(Now);
            for (var i = 0; i < DefaultOptions.MaxOtpAttempts; i++)
                await Authenticate(phoneNo, "0000", time);

            var later = new FakeTimeProvider(Now + DefaultOptions.OtpThrottle);
            using (var context = _fixture.CreateContext())
                await CreateService(context, later).GenerateOtpForUser(new OtpRequest { UserName = phoneNo });
            var newOtp = StoredUser(phoneNo).OneTimePassword!;

            Assert.Equal(AuthStatus.Success, (await Authenticate(phoneNo, newOtp, later)).Status);
        }

        [Fact]
        public async Task Authenticate_WrongPassword_WithoutActiveOtp_DoesNotCountAttempts()
        {
            var phoneNo = UniquePhoneNo();
            await SeedUser(phoneNo, password: "hemmelig");

            var result = await Authenticate(phoneNo, "feil");

            Assert.Equal(AuthStatus.AuthenticationFailed, result.Status);
            Assert.Equal(0, StoredUser(phoneNo).FailedOtpAttempts);
        }

        [Fact]
        public async Task Authenticate_WrongPassword_WithExpiredOtp_DoesNotCountAttempts()
        {
            var phoneNo = UniquePhoneNo();
            await SeedUser(phoneNo, otpCreated: Now.UtcDateTime);

            await Authenticate(phoneNo, "0000", new FakeTimeProvider(Now + DefaultOptions.OtpLifetime));

            var stored = StoredUser(phoneNo);
            Assert.Equal(0, stored.FailedOtpAttempts);
            Assert.Equal("1234", stored.OneTimePassword);
        }

        [Fact]
        public async Task Authenticate_CorrectPassword_Succeeds_WithActiveOtp_AndResetsOtp()
        {
            var phoneNo = UniquePhoneNo();
            await SeedUser(phoneNo, password: "hemmelig", otpCreated: Now.UtcDateTime, failedOtpAttempts: 2);

            var result = await Authenticate(phoneNo, "hemmelig", new FakeTimeProvider(Now));

            Assert.Equal(AuthStatus.Success, result.Status);
            var stored = StoredUser(phoneNo);
            Assert.Equal(0, stored.FailedOtpAttempts);
            Assert.Null(stored.OneTimePassword);
            Assert.Null(stored.OtpCreated);
        }

        [Fact]
        public async Task Authenticate_CorrectPassword_Succeeds_AfterOtpWasInvalidated()
        {
            // Feilforsøk mot koden (også feil passord mens koden er aktiv) sperrer ikke passordinnlogging.
            var phoneNo = UniquePhoneNo();
            await SeedUser(phoneNo, password: "hemmelig", otpCreated: Now.UtcDateTime);
            var time = new FakeTimeProvider(Now);
            for (var i = 0; i < DefaultOptions.MaxOtpAttempts; i++)
                await Authenticate(phoneNo, "feil", time);
            Assert.Null(StoredUser(phoneNo).OneTimePassword);

            Assert.Equal(AuthStatus.Success, (await Authenticate(phoneNo, "hemmelig", time)).Status);
        }
    }
}
