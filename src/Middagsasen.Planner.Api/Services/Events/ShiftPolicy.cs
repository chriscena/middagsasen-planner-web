using Middagsasen.Planner.Api.Data;

namespace Middagsasen.Planner.Api.Services.Events
{
    public enum ShiftUpdateAccess
    {
        /// <summary>Brukeren har ikke tilgang (403).</summary>
        Forbidden,
        /// <summary>Brukeren kan endre selve vakta (tider, kommentar, bruker) og opplæringen.</summary>
        Full,
        /// <summary>Brukeren kan bare oppdatere opplæringen til eieren av vakta (trener).</summary>
        TrainingOnly,
    }

    /// <summary>
    /// Tilgangsregler for vakter. Ren og uten avhengigheter: opplysninger som krever databaseoppslag
    /// (f.eks. om brukeren er trener) slås opp av servicen og sendes inn.
    /// Vurderingene gjøres mot den lagrede vakta, ikke mot verdiene i forespørselen.
    /// </summary>
    public static class ShiftPolicy
    {
        /// <summary>
        /// Ta en vakt. Admin kan sette opp hvem som helst. En vanlig bruker kan bare ta vakt for seg selv,
        /// og en eventuell opplæring som sendes med må gjelde samme bruker.
        /// </summary>
        /// <param name="shiftUserId">Brukeren vakta settes opp på.</param>
        /// <param name="trainingUserId">Brukeren opplæringen gjelder, eller <c>null</c> uten opplæring.</param>
        public static bool CanAdd(Actor actor, int shiftUserId, int? trainingUserId)
        {
            if (actor.IsAdmin) return true;
            return shiftUserId == actor.UserId
                && (trainingUserId is null || trainingUserId == shiftUserId);
        }

        /// <summary>
        /// Oppdatere en vakt. Tilgang:
        /// <list type="bullet">
        /// <item>Admin kan endre alt, også flytte vakta til en annen bruker (<see cref="ShiftUpdateAccess.Full"/>).</item>
        /// <item>Eieren kan endre tider, kommentar og egen opplæring, men ikke flytte vakta til en annen bruker
        /// (<see cref="ShiftUpdateAccess.Full"/>). Opplæringen må gjelde eieren selv.</item>
        /// <item>En trener for vaktas ressurstype kan bare oppdatere opplæringen til eieren, på vaktas ressurstype
        /// (<see cref="ShiftUpdateAccess.TrainingOnly"/>). Vaktfeltene (tider, kommentar, bruker) ignoreres stille,
        /// siden klienten sender hele objektet. Uten opplæring er kallet en no-op.</item>
        /// <item>Alle andre får <see cref="ShiftUpdateAccess.Forbidden"/>.</item>
        /// </list>
        /// </summary>
        /// <param name="shiftUserId">Eieren av den lagrede vakta.</param>
        /// <param name="shiftResourceTypeId">Ressurstypen til den lagrede vakta.</param>
        /// <param name="isTrainer">Om innlogget bruker er trener for vaktas ressurstype.</param>
        /// <param name="requestedUserId">Brukeren forespørselen vil sette på vakta.</param>
        /// <param name="training">Brukeren og ressurstypen opplæringen i forespørselen gjelder, eller <c>null</c> uten opplæring.</param>
        public static ShiftUpdateAccess CanUpdate(
            Actor actor,
            int shiftUserId,
            int shiftResourceTypeId,
            bool isTrainer,
            int requestedUserId,
            (int UserId, int ResourceTypeId)? training)
        {
            if (actor.IsAdmin) return ShiftUpdateAccess.Full;

            if (shiftUserId == actor.UserId)
            {
                // Eieren kan ikke flytte vakta til en annen bruker, og opplæringen må gjelde eieren selv.
                if (requestedUserId != actor.UserId) return ShiftUpdateAccess.Forbidden;
                if (training is { } own && own.UserId != shiftUserId) return ShiftUpdateAccess.Forbidden;
                return ShiftUpdateAccess.Full;
            }

            if (!isTrainer) return ShiftUpdateAccess.Forbidden;

            // Treneren kan bare oppdatere opplæringen til eieren av vakta, på vaktas ressurstype.
            if (training is { } t && (t.UserId != shiftUserId || t.ResourceTypeId != shiftResourceTypeId))
                return ShiftUpdateAccess.Forbidden;

            return ShiftUpdateAccess.TrainingOnly;
        }

        /// <summary>Slette en vakt: admin eller eieren av vakta.</summary>
        public static bool CanDelete(Actor actor, EventResourceUser shift)
            => actor.IsAdminOrSelf(shift.UserId);
    }
}
