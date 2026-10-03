using Middagsasen.Planner.Api.Services;
using Middagsasen.Planner.Api.Services.Competencies;

namespace Middagsasen.Planner.Api.Tests.Services.Competencies
{
    public class CompetencyPolicyTests
    {
        private const int UserId = 10;
        private const int OtherId = 20;

        // Kolonner: innlogget bruker, admin, forventet
        [Theory]
        [InlineData(UserId, false, true)]
        [InlineData(OtherId, false, false)]
        [InlineData(OtherId, true, true)]
        public void CanReadUserCompetencies(int actorId, bool isAdmin, bool expected)
        {
            Assert.Equal(expected, CompetencyPolicy.CanReadUserCompetencies(new Actor(actorId, isAdmin), UserId));
        }

        [Theory]
        [InlineData(UserId, false, true)]
        [InlineData(OtherId, false, false)]
        [InlineData(OtherId, true, true)]
        public void CanAddUserCompetency(int actorId, bool isAdmin, bool expected)
        {
            Assert.Equal(expected, CompetencyPolicy.CanAddUserCompetency(new Actor(actorId, isAdmin), UserId));
        }

        // Kolonner: innlogget bruker, admin, aktiv godkjenner, forventet. Kompetansen tilhører UserId.
        [Theory]
        [InlineData(OtherId, false, false, false)]
        [InlineData(OtherId, false, true, true)]   // godkjenner på en annens kompetanse
        [InlineData(OtherId, true, false, true)]
        [InlineData(OtherId, true, true, true)]
        [InlineData(UserId, false, true, false)]   // godkjenner kan ikke godkjenne egen kompetanse
        [InlineData(UserId, false, false, false)]
        [InlineData(UserId, true, false, true)]    // admin kan godkjenne egen kompetanse
        [InlineData(UserId, true, true, true)]
        public void CanApprove(int actorId, bool isAdmin, bool isApprover, bool expected)
        {
            Assert.Equal(expected, CompetencyPolicy.CanApprove(new Actor(actorId, isAdmin), UserId, isApprover));
        }
    }
}
