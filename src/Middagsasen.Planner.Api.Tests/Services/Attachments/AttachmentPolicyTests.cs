using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services;
using Middagsasen.Planner.Api.Services.Attachments;

namespace Middagsasen.Planner.Api.Tests.Services.Attachments
{
    public class AttachmentPolicyTests
    {
        private static readonly Actor User = new(10, false);
        private static readonly Actor Admin = new(20, true);

        [Fact]
        public void CanManage_OnlyAdmin()
        {
            Assert.False(AttachmentPolicy.CanManage(User));
            Assert.True(AttachmentPolicy.CanManage(Admin));
        }

        // Kolonner: admin, arkivert, forventet
        [Theory]
        [InlineData(false, false, true)]
        [InlineData(false, true, false)]
        [InlineData(true, false, true)]
        [InlineData(true, true, true)]
        public void CanDownload_ArchivedOnlyForAdmin(bool isAdmin, bool isArchived, bool expected)
        {
            Assert.Equal(expected, AttachmentPolicy.CanDownload(isAdmin ? Admin : User, isArchived));
        }

        [Fact]
        public void CanViewReport_OnlyAdmin()
        {
            Assert.False(AttachmentPolicy.CanViewReport(User));
            Assert.True(AttachmentPolicy.CanViewReport(Admin));
        }

        // Kolonner: krever kvittering, arkivert, forventet
        [Theory]
        [InlineData(true, false, true)]
        [InlineData(true, true, false)]
        [InlineData(false, false, false)]
        [InlineData(false, true, false)]
        public void CanAcknowledge_ActiveInstructionsOnly(bool requiresAcknowledgement, bool archived, bool expected)
        {
            var file = new ResourceTypeFile
            {
                RequiresAcknowledgement = requiresAcknowledgement,
                Archived = archived ? new DateTime(2026, 1, 1) : null,
            };

            Assert.Equal(expected, AttachmentPolicy.CanAcknowledge(file));
        }
    }
}
