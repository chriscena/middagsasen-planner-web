using Middagsasen.Planner.Api.Core;
using Middagsasen.Planner.Api.Data;

namespace Middagsasen.Planner.Api.Services.Attachments
{
    /// <summary>
    /// Regler for instrukser. Ren og uten avhengigheter: filene, kvitteringene og «nå» slås opp av kalleren og sendes inn.
    /// </summary>
    public static class AttachmentRules
    {
        /// <summary>
        /// Instruksene som sperrer brukeren fra å ta vakt på vakttypen: aktive, påkrevde instrukser brukeren ikke har
        /// kvittert for, sortert på beskrivelse. Tom liste når ingenting sperrer.
        /// </summary>
        /// <param name="files">Vedleggene på vakttypen (aktive og arkiverte; arkiverte hoppes over).</param>
        /// <param name="acknowledgedFileIds">Vedleggene brukeren har kvittert for.</param>
        public static IReadOnlyList<ResourceTypeFile> BlockingInstructions(IEnumerable<ResourceTypeFile> files, IReadOnlySet<int> acknowledgedFileIds)
            => files
                .Where(f => IsBlocking(f, acknowledgedFileIds))
                .OrderBy(f => f.Description, StringComparer.OrdinalIgnoreCase)
                .ThenBy(f => f.ResourceTypeFileId)
                .ToList();

        /// <summary>
        /// Om noe sperrer: samme regel som <see cref="BlockingInstructions"/>, men uten å bygge og sortere listen. Brukes
        /// til flaggene, som regnes ut per oppgave og vakt.
        /// </summary>
        public static bool HasBlockingInstruction(IEnumerable<ResourceTypeFile> files, IReadOnlySet<int> acknowledgedFileIds)
            => files.Any(f => IsBlocking(f, acknowledgedFileIds));

        private static bool IsBlocking(ResourceTypeFile file, IReadOnlySet<int> acknowledgedFileIds)
            => file.Archived == null && file.RequiresAcknowledgement && file.IsMandatory && !acknowledgedFileIds.Contains(file.ResourceTypeFileId);

        /// <summary>
        /// Vakttypen gjelder brukeren: brukeren har vakt på den fra og med inneværende sesongstart. Brukes både for
        /// <c>appliesToMe</c> i instrukslisten og for standardpopulasjonen i kvitteringsrapporten.
        /// </summary>
        /// <param name="resourceTypeIdsWithShiftsThisSeason">Vakttypene brukeren har vakt på denne sesongen.</param>
        public static bool AppliesTo(int resourceTypeId, IReadOnlySet<int> resourceTypeIdsWithShiftsThisSeason)
            => resourceTypeIdsWithShiftsThisSeason.Contains(resourceTypeId);

        /// <summary>
        /// Starten på inneværende sesong (1. juli 00:00 norsk tid) som UTC-tidspunkt, vurdert ut fra <paramref name="now"/>.
        /// Oppgavenes tider lagres som norsk lokal tid uten sone, så den som sammenligner med dem må konvertere med
        /// <see cref="DateTimeExtensions.ToNorwegianLocalTime"/>.
        /// </summary>
        public static DateTime SeasonStartUtc(DateTimeOffset now)
            => DateTimeExtensions.GetSeasonRangeUtc(now.GetSeasonStartYear()).From;
    }
}
