using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services.Attachments;

namespace Middagsasen.Planner.Api.Tests.Services.Attachments
{
    public class AttachmentRulesTests
    {
        private static ResourceTypeFile File(int id, string description, bool requiresAcknowledgement = true, bool isMandatory = true, bool archived = false) => new()
        {
            ResourceTypeFileId = id,
            Description = description,
            RequiresAcknowledgement = requiresAcknowledgement,
            IsMandatory = isMandatory,
            Archived = archived ? new DateTime(2026, 1, 1) : null,
        };

        [Fact]
        public void BlockingInstructions_ActiveMandatoryUnacknowledged_SortedByDescription()
        {
            var files = new[]
            {
                File(1, "Sikkerhetsrutine"),
                File(2, "Heisførerinstruks"),
                File(3, "Kvittert instruks"),
                File(4, "Løypekart", requiresAcknowledgement: false, isMandatory: false),
                File(5, "Anbefalt lesning", isMandatory: false),
                File(6, "Arkivert instruks", archived: true),
                // Ugyldig kombinasjon (skal avvises av servicen), men sperrer ikke om den skulle finnes.
                File(7, "Påkrevd uten kvittering", requiresAcknowledgement: false, isMandatory: true),
            };

            var blocking = AttachmentRules.BlockingInstructions(files, new HashSet<int> { 3 });

            Assert.Equal(["Heisførerinstruks", "Sikkerhetsrutine"], blocking.Select(f => f.Description));
        }

        [Fact]
        public void BlockingInstructions_Empty_WhenAllAcknowledged_OrNoneMandatory()
        {
            Assert.Empty(AttachmentRules.BlockingInstructions([File(1, "A"), File(2, "B")], new HashSet<int> { 1, 2 }));
            Assert.Empty(AttachmentRules.BlockingInstructions([File(1, "A", isMandatory: false)], new HashSet<int>()));
            Assert.Empty(AttachmentRules.BlockingInstructions([], new HashSet<int>()));
        }

        [Fact]
        public void AppliesTo_WhenUserHasShiftOnResourceTypeThisSeason()
        {
            var thisSeason = new HashSet<int> { 1, 3 };

            Assert.True(AttachmentRules.AppliesTo(1, thisSeason));
            Assert.False(AttachmentRules.AppliesTo(2, thisSeason));
            Assert.False(AttachmentRules.AppliesTo(1, new HashSet<int>()));
        }

        [Theory]
        // Kolonner: nå (UTC), forventet sesongstart (UTC). 1. juli 00:00 norsk sommertid = 30. juni 22:00Z.
        [InlineData("2026-10-11T12:00:00Z", "2026-06-30T22:00:00Z")]
        [InlineData("2026-03-01T12:00:00Z", "2025-06-30T22:00:00Z")]
        [InlineData("2026-06-30T21:59:59Z", "2025-06-30T22:00:00Z")] // like før sesongskiftet i norsk tid
        [InlineData("2026-06-30T22:00:00Z", "2026-06-30T22:00:00Z")] // akkurat ved sesongskiftet
        public void SeasonStartUtc_IsJulyFirstNorwegianTime(string now, string expected)
        {
            var start = AttachmentRules.SeasonStartUtc(DateTimeOffset.Parse(now));

            Assert.Equal(DateTimeOffset.Parse(expected).UtcDateTime, start);
            Assert.Equal(DateTimeKind.Utc, start.Kind);
        }
    }
}
