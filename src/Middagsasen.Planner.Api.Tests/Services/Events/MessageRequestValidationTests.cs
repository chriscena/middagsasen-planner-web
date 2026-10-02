using System.ComponentModel.DataAnnotations;
using Middagsasen.Planner.Api.Services.Events;

namespace Middagsasen.Planner.Api.Tests.Services.Events
{
    public class MessageRequestValidationTests
    {
        private static bool IsValid(MessageRequest request, out List<ValidationResult> results)
        {
            results = [];
            return Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Message_IsRejected_WhenNullEmptyOrWhitespace(string? message)
        {
            var request = new MessageRequest { Message = message! };

            var valid = IsValid(request, out var results);

            Assert.False(valid);
            var result = Assert.Single(results);
            Assert.Contains(nameof(MessageRequest.Message), result.MemberNames);
        }

        [Fact]
        public void Message_IsAccepted_WhenNotBlank()
        {
            var request = new MessageRequest { Message = "hei" };

            var valid = IsValid(request, out var results);

            Assert.True(valid);
            Assert.Empty(results);
        }

        [Fact]
        public void Message_IsAccepted_WhenExactlyMaxLength()
        {
            var request = new MessageRequest { Message = new string('a', MessageRequest.MaxLength) };

            var valid = IsValid(request, out var results);

            Assert.Equal(4000, MessageRequest.MaxLength);
            Assert.True(valid);
            Assert.Empty(results);
        }

        [Fact]
        public void Message_IsRejected_WhenLongerThanMaxLength()
        {
            var request = new MessageRequest { Message = new string('a', MessageRequest.MaxLength + 1) };

            var valid = IsValid(request, out var results);

            Assert.False(valid);
            var result = Assert.Single(results);
            Assert.Contains(nameof(MessageRequest.Message), result.MemberNames);
        }
    }
}
