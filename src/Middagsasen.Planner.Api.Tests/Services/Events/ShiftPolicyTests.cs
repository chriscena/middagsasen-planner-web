using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services;
using Middagsasen.Planner.Api.Services.Events;

namespace Middagsasen.Planner.Api.Tests.Services.Events
{
    public class ShiftPolicyTests
    {
        private const int OwnerId = 10;
        private const int OtherId = 20;
        private const int ResourceTypeId = 5;
        private const int OtherResourceTypeId = 6;

        private static readonly Actor Owner = new(OwnerId, false);
        private static readonly Actor Other = new(OtherId, false);
        private static readonly Actor Admin = new(OtherId, true);

        // --- CanAdd ---

        [Theory]
        // Kolonner: innlogget bruker, admin, vaktas bruker, opplæringens bruker (null = ingen), forventet
        [InlineData(OwnerId, false, OwnerId, null, true)]
        [InlineData(OwnerId, false, OwnerId, OwnerId, true)]
        [InlineData(OwnerId, false, OwnerId, OtherId, false)]
        [InlineData(OwnerId, false, OtherId, null, false)]
        [InlineData(OwnerId, false, OtherId, OtherId, false)]
        [InlineData(OwnerId, true, OtherId, null, true)]
        [InlineData(OwnerId, true, OtherId, OtherId, true)]
        [InlineData(OwnerId, true, OwnerId, OtherId, true)]
        public void CanAdd(int actorId, bool isAdmin, int shiftUserId, int? trainingUserId, bool expected)
        {
            Assert.Equal(expected, ShiftPolicy.CanAdd(new Actor(actorId, isAdmin), shiftUserId, trainingUserId));
        }

        // --- CanUpdate ---

        public enum Who { Admin, Owner, OwnerAndTrainer, Trainer, Other }

        public enum Training { None, ForOwner, ForOtherUser, ForOtherResourceType }

        private static (Actor Actor, bool IsTrainer) Resolve(Who who) => who switch
        {
            Who.Admin => (Admin, false),
            Who.Owner => (Owner, false),
            Who.OwnerAndTrainer => (Owner, true),
            Who.Trainer => (Other, true),
            Who.Other => (Other, false),
            _ => throw new ArgumentOutOfRangeException(nameof(who)),
        };

        private static (int UserId, int ResourceTypeId)? Resolve(Training training) => training switch
        {
            Training.None => null,
            Training.ForOwner => (OwnerId, ResourceTypeId),
            Training.ForOtherUser => (OtherId, ResourceTypeId),
            Training.ForOtherResourceType => (OwnerId, OtherResourceTypeId),
            _ => throw new ArgumentOutOfRangeException(nameof(training)),
        };

        // Kolonner: hvem, om requesten flytter vakta til en annen bruker, opplæring, forventet
        [Theory]
        // --- Admin: alltid full tilgang ---
        [InlineData(Who.Admin, false, Training.None, ShiftUpdateAccess.Full)]
        [InlineData(Who.Admin, true, Training.None, ShiftUpdateAccess.Full)]
        [InlineData(Who.Admin, false, Training.ForOwner, ShiftUpdateAccess.Full)]
        [InlineData(Who.Admin, false, Training.ForOtherUser, ShiftUpdateAccess.Full)]
        [InlineData(Who.Admin, false, Training.ForOtherResourceType, ShiftUpdateAccess.Full)]
        // --- Eier: full tilgang, men kan ikke flytte vakta eller sette opplæring for andre ---
        [InlineData(Who.Owner, false, Training.None, ShiftUpdateAccess.Full)]
        [InlineData(Who.Owner, false, Training.ForOwner, ShiftUpdateAccess.Full)]
        [InlineData(Who.Owner, false, Training.ForOtherResourceType, ShiftUpdateAccess.Full)]
        [InlineData(Who.Owner, true, Training.None, ShiftUpdateAccess.Forbidden)]
        [InlineData(Who.Owner, true, Training.ForOwner, ShiftUpdateAccess.Forbidden)]
        [InlineData(Who.Owner, false, Training.ForOtherUser, ShiftUpdateAccess.Forbidden)]
        // --- Eier som også er trener: eierreglene gjelder ---
        [InlineData(Who.OwnerAndTrainer, false, Training.ForOwner, ShiftUpdateAccess.Full)]
        [InlineData(Who.OwnerAndTrainer, true, Training.None, ShiftUpdateAccess.Forbidden)]
        [InlineData(Who.OwnerAndTrainer, false, Training.ForOtherUser, ShiftUpdateAccess.Forbidden)]
        // --- Trener for vaktas ressurstype: bare opplæringen til eieren, på vaktas ressurstype ---
        [InlineData(Who.Trainer, false, Training.None, ShiftUpdateAccess.TrainingOnly)]
        [InlineData(Who.Trainer, true, Training.None, ShiftUpdateAccess.TrainingOnly)]
        [InlineData(Who.Trainer, false, Training.ForOwner, ShiftUpdateAccess.TrainingOnly)]
        [InlineData(Who.Trainer, true, Training.ForOwner, ShiftUpdateAccess.TrainingOnly)]
        [InlineData(Who.Trainer, false, Training.ForOtherUser, ShiftUpdateAccess.Forbidden)]
        [InlineData(Who.Trainer, false, Training.ForOtherResourceType, ShiftUpdateAccess.Forbidden)]
        // --- Alle andre ---
        [InlineData(Who.Other, false, Training.None, ShiftUpdateAccess.Forbidden)]
        [InlineData(Who.Other, true, Training.None, ShiftUpdateAccess.Forbidden)]
        [InlineData(Who.Other, false, Training.ForOwner, ShiftUpdateAccess.Forbidden)]
        public void CanUpdate(Who who, bool movesShift, Training training, ShiftUpdateAccess expected)
        {
            var (actor, isTrainer) = Resolve(who);
            // «Flytter vakta» betyr at requesten setter en annen bruker enn den innloggede.
            var requestedUserId = movesShift ? actor.UserId + 1000 : actor.UserId;
            // Admin og trener sender typisk eierens id; det skal uansett ikke påvirke utfallet for dem.
            if (!movesShift && who is (Who.Admin or Who.Trainer)) requestedUserId = OwnerId;

            var result = ShiftPolicy.CanUpdate(actor, OwnerId, ResourceTypeId, isTrainer, requestedUserId, Resolve(training));

            Assert.Equal(expected, result);
        }

        // --- CanDelete ---

        [Theory]
        [InlineData(OwnerId, false, true)]
        [InlineData(OtherId, false, false)]
        [InlineData(OtherId, true, true)]
        public void CanDelete(int actorId, bool isAdmin, bool expected)
        {
            var shift = new EventResourceUser { EventResourceUserId = 1, UserId = OwnerId };

            Assert.Equal(expected, ShiftPolicy.CanDelete(new Actor(actorId, isAdmin), shift));
        }
    }
}
