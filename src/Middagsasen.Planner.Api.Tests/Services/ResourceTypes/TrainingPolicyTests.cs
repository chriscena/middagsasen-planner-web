using Middagsasen.Planner.Api.Services;
using Middagsasen.Planner.Api.Services.ResourceTypes;

namespace Middagsasen.Planner.Api.Tests.Services.ResourceTypes
{
    public class TrainingPolicyTests
    {
        private const int TraineeId = 10;
        private const int OtherId = 20;

        // Kolonner: innlogget bruker, admin, trener for ressurstypen, forventet
        [Theory]
        // Brukeren selv (inkludert selverklæringen «trenger ikke opplæring»)
        [InlineData(TraineeId, false, false, true)]
        // Admin
        [InlineData(OtherId, true, false, true)]
        // Trener for ressurstypen
        [InlineData(OtherId, false, true, true)]
        // Alle andre
        [InlineData(OtherId, false, false, false)]
        public void CanManage(int actorId, bool isAdmin, bool isTrainer, bool expected)
        {
            Assert.Equal(expected, TrainingPolicy.CanManage(new Actor(actorId, isAdmin), TraineeId, isTrainer));
        }
    }
}
