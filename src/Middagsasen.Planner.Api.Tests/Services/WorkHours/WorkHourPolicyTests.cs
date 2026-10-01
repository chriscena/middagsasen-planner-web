using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services.WorkHours;

namespace Middagsasen.Planner.Api.Tests.Services.WorkHours
{
    public class WorkHourPolicyTests
    {
        private const int OwnerId = 10;
        private const int OtherId = 20;

        public enum Action { Edit, Delete, Approve, Reject, Unlock }

        private const int Approved = 1;
        private const int Rejected = 2;

        private static WorkHour Entry(int? status) => new() { WorkHourId = 1, UserId = OwnerId, ApprovalStatus = status };

        private static WorkHourAccess Evaluate(WorkHour entry, bool isAdmin, int userId, Action action) => action switch
        {
            Action.Edit or Action.Delete => WorkHourPolicy.CanEdit(entry, isAdmin, userId),
            Action.Approve => WorkHourPolicy.CanSetStatus(entry, isAdmin, userId, 1),
            Action.Reject => WorkHourPolicy.CanSetStatus(entry, isAdmin, userId, 2),
            Action.Unlock => WorkHourPolicy.CanSetStatus(entry, isAdmin, userId, null),
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };

        // Kolonner: eier, admin, status (null = åpen, 1 = godkjent, 2 = avslått), handling, forventet
        [Theory]
        // --- Eier, vanlig bruker ---
        [InlineData(true, false, null, Action.Edit, WorkHourAccess.Allowed)]
        [InlineData(true, false, null, Action.Delete, WorkHourAccess.Allowed)]
        [InlineData(true, false, null, Action.Approve, WorkHourAccess.Forbidden)]
        [InlineData(true, false, null, Action.Reject, WorkHourAccess.Forbidden)]
        [InlineData(true, false, null, Action.Unlock, WorkHourAccess.Forbidden)]
        [InlineData(true, false, Approved, Action.Edit, WorkHourAccess.Locked)]
        [InlineData(true, false, Approved, Action.Delete, WorkHourAccess.Locked)]
        [InlineData(true, false, Approved, Action.Approve, WorkHourAccess.Forbidden)]
        [InlineData(true, false, Approved, Action.Reject, WorkHourAccess.Forbidden)]
        [InlineData(true, false, Approved, Action.Unlock, WorkHourAccess.Forbidden)]
        [InlineData(true, false, Rejected, Action.Edit, WorkHourAccess.Locked)]
        [InlineData(true, false, Rejected, Action.Delete, WorkHourAccess.Locked)]
        [InlineData(true, false, Rejected, Action.Approve, WorkHourAccess.Forbidden)]
        [InlineData(true, false, Rejected, Action.Reject, WorkHourAccess.Forbidden)]
        [InlineData(true, false, Rejected, Action.Unlock, WorkHourAccess.Forbidden)]
        // --- Ikke-eier, vanlig bruker: alltid forbudt ---
        [InlineData(false, false, null, Action.Edit, WorkHourAccess.Forbidden)]
        [InlineData(false, false, null, Action.Delete, WorkHourAccess.Forbidden)]
        [InlineData(false, false, null, Action.Approve, WorkHourAccess.Forbidden)]
        [InlineData(false, false, null, Action.Reject, WorkHourAccess.Forbidden)]
        [InlineData(false, false, null, Action.Unlock, WorkHourAccess.Forbidden)]
        [InlineData(false, false, Approved, Action.Edit, WorkHourAccess.Forbidden)]
        [InlineData(false, false, Approved, Action.Delete, WorkHourAccess.Forbidden)]
        [InlineData(false, false, Approved, Action.Approve, WorkHourAccess.Forbidden)]
        [InlineData(false, false, Approved, Action.Reject, WorkHourAccess.Forbidden)]
        [InlineData(false, false, Approved, Action.Unlock, WorkHourAccess.Forbidden)]
        [InlineData(false, false, Rejected, Action.Edit, WorkHourAccess.Forbidden)]
        [InlineData(false, false, Rejected, Action.Delete, WorkHourAccess.Forbidden)]
        [InlineData(false, false, Rejected, Action.Approve, WorkHourAccess.Forbidden)]
        [InlineData(false, false, Rejected, Action.Reject, WorkHourAccess.Forbidden)]
        [InlineData(false, false, Rejected, Action.Unlock, WorkHourAccess.Forbidden)]
        // --- Ikke-eier, admin ---
        [InlineData(false, true, null, Action.Edit, WorkHourAccess.Allowed)]
        [InlineData(false, true, null, Action.Delete, WorkHourAccess.Allowed)]
        [InlineData(false, true, null, Action.Approve, WorkHourAccess.Allowed)]
        [InlineData(false, true, null, Action.Reject, WorkHourAccess.Allowed)]
        [InlineData(false, true, null, Action.Unlock, WorkHourAccess.Locked)]
        [InlineData(false, true, Approved, Action.Edit, WorkHourAccess.Locked)]
        [InlineData(false, true, Approved, Action.Delete, WorkHourAccess.Locked)]
        [InlineData(false, true, Approved, Action.Approve, WorkHourAccess.Locked)]
        [InlineData(false, true, Approved, Action.Reject, WorkHourAccess.Locked)]
        [InlineData(false, true, Approved, Action.Unlock, WorkHourAccess.Allowed)]
        [InlineData(false, true, Rejected, Action.Edit, WorkHourAccess.Locked)]
        [InlineData(false, true, Rejected, Action.Delete, WorkHourAccess.Locked)]
        [InlineData(false, true, Rejected, Action.Approve, WorkHourAccess.Locked)]
        [InlineData(false, true, Rejected, Action.Reject, WorkHourAccess.Locked)]
        [InlineData(false, true, Rejected, Action.Unlock, WorkHourAccess.Allowed)]
        // --- Eier, admin (samme som admin) ---
        [InlineData(true, true, null, Action.Edit, WorkHourAccess.Allowed)]
        [InlineData(true, true, null, Action.Delete, WorkHourAccess.Allowed)]
        [InlineData(true, true, null, Action.Approve, WorkHourAccess.Allowed)]
        [InlineData(true, true, null, Action.Reject, WorkHourAccess.Allowed)]
        [InlineData(true, true, null, Action.Unlock, WorkHourAccess.Locked)]
        [InlineData(true, true, Approved, Action.Edit, WorkHourAccess.Locked)]
        [InlineData(true, true, Approved, Action.Delete, WorkHourAccess.Locked)]
        [InlineData(true, true, Approved, Action.Approve, WorkHourAccess.Locked)]
        [InlineData(true, true, Approved, Action.Reject, WorkHourAccess.Locked)]
        [InlineData(true, true, Approved, Action.Unlock, WorkHourAccess.Allowed)]
        [InlineData(true, true, Rejected, Action.Edit, WorkHourAccess.Locked)]
        [InlineData(true, true, Rejected, Action.Delete, WorkHourAccess.Locked)]
        [InlineData(true, true, Rejected, Action.Approve, WorkHourAccess.Locked)]
        [InlineData(true, true, Rejected, Action.Reject, WorkHourAccess.Locked)]
        [InlineData(true, true, Rejected, Action.Unlock, WorkHourAccess.Allowed)]
        public void RuleTable(bool isOwner, bool isAdmin, int? status, Action action, WorkHourAccess expected)
        {
            var userId = isOwner ? OwnerId : OtherId;

            var result = Evaluate(Entry(status), isAdmin, userId, action);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(true, false, null, true)]
        [InlineData(true, false, Approved, true)]
        [InlineData(true, false, Rejected, true)]
        [InlineData(false, false, null, false)]
        [InlineData(false, false, Approved, false)]
        [InlineData(false, false, Rejected, false)]
        [InlineData(false, true, null, true)]
        [InlineData(false, true, Approved, true)]
        [InlineData(false, true, Rejected, true)]
        [InlineData(true, true, null, true)]
        public void CanRead(bool isOwner, bool isAdmin, int? status, bool expected)
        {
            var userId = isOwner ? OwnerId : OtherId;

            Assert.Equal(expected, WorkHourPolicy.CanRead(Entry(status), isAdmin, userId));
        }

        // Policyen vurderer kun tilgang/tilstand; verdien valideres av servicen etterpå (403 → 409 → 400).
        [Theory]
        [InlineData(0, false, null, WorkHourAccess.Forbidden)]
        [InlineData(3, false, null, WorkHourAccess.Forbidden)]
        [InlineData(3, false, Approved, WorkHourAccess.Forbidden)]
        [InlineData(0, true, null, WorkHourAccess.Allowed)]
        [InlineData(3, true, null, WorkHourAccess.Allowed)]
        [InlineData(-1, true, null, WorkHourAccess.Allowed)]
        [InlineData(3, true, Approved, WorkHourAccess.Locked)]
        public void CanSetStatus_IgnoresStatusValue(int status, bool isAdmin, int? current, WorkHourAccess expected)
        {
            Assert.Equal(expected, WorkHourPolicy.CanSetStatus(Entry(current), isAdmin, OwnerId, status));
        }
    }
}
