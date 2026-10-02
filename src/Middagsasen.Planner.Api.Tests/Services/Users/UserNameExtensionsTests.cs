using Middagsasen.Planner.Api.Services.Users;

namespace Middagsasen.Planner.Api.Tests.Services.Users
{
    public class UserNameExtensionsTests
    {
        [Theory]
        [InlineData("92345678", "92345678")]
        [InlineData("10000000", "10000000")]
        [InlineData("99999999", "99999999")]
        [InlineData("923 45 678", "92345678")]
        [InlineData("+47 923 45 678", "92345678")]
        [InlineData("+4792345678", "92345678")]
        [InlineData("4792345678", "92345678")]
        [InlineData("0047 92345678", "92345678")]
        [InlineData("004792345678", "92345678")]
        [InlineData(" 92345678 ", "92345678")]
        [InlineData("0000000092345678", "92345678")]
        [InlineData("47123456", "47123456")]
        [InlineData("4747123456", "47123456")]
        [InlineData("4710000000", "10000000")]
        [InlineData("4799999999", "99999999")]
        public void ToNormalizedUserName_AcceptsNorwegianNumbers(string input, string expected)
        {
            Assert.Equal(expected, input.ToNormalizedUserName());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("admin")]
        [InlineData("1234")]
        [InlineData("9999999")]
        [InlineData("00000000")]
        [InlineData("01234567")]
        [InlineData("123456789")]
        [InlineData("47 9234567")]
        [InlineData("4692345678")]
        [InlineData("+46 92345678")]
        [InlineData("0046 92345678")]
        [InlineData("4701234567")]
        [InlineData("47 01234567")]
        [InlineData("+47 4792345678")]
        [InlineData("479234567890")]
        [InlineData("9223372036854775808")]
        [InlineData("12345678901234567890123")]
        [InlineData("٩٢٣٤٥٦٧٨")] // Andre sifre enn 0-9 (arabisk-indiske) regnes ikke som sifre, som i pre-deploy-skriptet.
        public void ToNormalizedUserName_ReturnsNull_ForInvalidOrForeignNumbers(string? input)
        {
            Assert.Null(input.ToNormalizedUserName());
        }

        [Fact]
        public void ToSmsPhoneNo_AddsCountryCode()
        {
            Assert.Equal(4792345678L, "92345678".ToSmsPhoneNo());
        }
    }
}
