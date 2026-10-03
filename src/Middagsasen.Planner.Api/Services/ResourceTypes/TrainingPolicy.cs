namespace Middagsasen.Planner.Api.Services.ResourceTypes
{
    /// <summary>
    /// Tilgangsregler for opplæring på ressurstyper. Ren og uten avhengigheter: om brukeren er trener
    /// slås opp av servicen og sendes inn. Ved oppdatering vurderes tilgangen mot den lagrede opplæringen.
    /// </summary>
    public static class TrainingPolicy
    {
        /// <summary>
        /// Opprette eller oppdatere opplæring. Tilgang har:
        /// <list type="bullet">
        /// <item>Admin, for alle brukere.</item>
        /// <item>En trener for ressurstypen, for alle brukere på den ressurstypen.</item>
        /// <item>Brukeren selv, for egen opplæring.</item>
        /// </list>
        /// <para>
        /// Merk: en vanlig bruker kan bevisst sette <c>TrainingCompleted</c> til både <c>true</c> og <c>false</c>
        /// på egen opplæring. <c>true</c> er selverklæringen «trenger ikke opplæring» som klienten sender når
        /// en vakt tas, og <c>false</c> er en forespørsel om opplæring som varsler trenerne. Dette er en bevisst
        /// beslutning (issue #96) om å beholde dagens oppførsel, selv om en strengere regel der bare trener/admin
        /// kan bekrefte gjennomført opplæring ble vurdert.
        /// </para>
        /// </summary>
        /// <param name="trainingUserId">Brukeren opplæringen gjelder.</param>
        /// <param name="isTrainerForResourceType">Om innlogget bruker er trener for opplæringens ressurstype.</param>
        public static bool CanManage(Actor actor, int trainingUserId, bool isTrainerForResourceType)
            => actor.IsAdminOrSelf(trainingUserId) || isTrainerForResourceType;
    }
}
