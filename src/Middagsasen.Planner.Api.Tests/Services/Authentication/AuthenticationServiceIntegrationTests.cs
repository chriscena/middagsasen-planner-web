using Microsoft.EntityFrameworkCore;
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

        private AuthenticationService CreateService(PlannerDbContext context)
        {
            var authSettings = Substitute.For<IAuthSettings>();
            authSettings.Secret.Returns(new string('x', 64));
            return new AuthenticationService(context, _smsSender, authSettings);
        }

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

        private async Task<User> SeedUser(string userName, bool inactive = false, string? password = null)
        {
            using var context = _fixture.CreateContext();
            var user = new User
            {
                UserName = userName,
                FirstName = "Eksisterende",
                LastName = "Bruker",
                Inactive = inactive,
                Created = DateTime.UtcNow,
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
        public async Task GenerateOtpForUser_ReturnsTooManyRequests_WhenUserIsCreatedConcurrently()
        {
            var phoneNo = UniquePhoneNo();

            // En parallell forespørsel oppretter brukeren mellom oppslaget og lagringen.
            using var context = _fixture.CreateContext(new BeforeSaveInterceptor(() => SeedUser(phoneNo)));
            var result = await CreateService(context).GenerateOtpForUser(new OtpRequest { UserName = phoneNo });

            Assert.Equal(OtpStatus.TooManyRequests, result.Status);
            Assert.Single(UsersWithUserName(phoneNo));
            await _smsSender.DidNotReceive().SendMessages(Arg.Any<IEnumerable<SmsMessage>>());
        }

        [Fact]
        public async Task GenerateOtpForUser_ReturnsInvalidPhoneNumber_ForInvalidInput()
        {
            using var context = _fixture.CreateContext();
            var result = await CreateService(context).GenerateOtpForUser(new OtpRequest { UserName = "1234" });

            Assert.Equal(OtpStatus.InvalidPhoneNumber, result.Status);
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
    }
}
