using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services;
using Middagsasen.Planner.Api.Services.Events;

namespace Middagsasen.Planner.Api.Tests.Services.Events
{
    public class MessagePolicyTests
    {
        private const int AuthorId = 10;
        private const int OtherId = 20;

        // Kolonner: innlogget bruker, admin, forventet
        [Theory]
        [InlineData(AuthorId, false, true)]
        [InlineData(AuthorId, true, true)]
        [InlineData(OtherId, false, false)]
        [InlineData(OtherId, true, true)]
        public void CanDelete(int actorId, bool isAdmin, bool expected)
        {
            var message = new EventResourceMessage { EventResourceMessageId = 1, CreatedBy = AuthorId, Message = "Hei" };

            Assert.Equal(expected, MessagePolicy.CanDelete(new Actor(actorId, isAdmin), message));
        }
    }
}
