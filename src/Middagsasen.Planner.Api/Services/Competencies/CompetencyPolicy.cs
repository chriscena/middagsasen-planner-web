namespace Middagsasen.Planner.Api.Services.Competencies
{
    /// <summary>
    /// Tilgangsregler for brukerkompetanser. Ren og uten avhengigheter: om brukeren er godkjenner
    /// slås opp av servicen og sendes inn.
    /// </summary>
    public static class CompetencyPolicy
    {
        /// <summary>Lese en brukers kompetanser: admin eller brukeren selv.</summary>
        public static bool CanReadUserCompetencies(Actor actor, int userId)
            => actor.IsAdminOrSelf(userId);

        /// <summary>Registrere en kompetanse (ikke godkjent) på en bruker: admin eller brukeren selv.</summary>
        public static bool CanAddUserCompetency(Actor actor, int userId)
            => actor.IsAdminOrSelf(userId);

        /// <summary>
        /// Godkjenne en brukerkompetanse: admin, eller aktiv godkjenner for kompetansen når kompetansen
        /// tilhører en annen bruker. En godkjenner kan ikke godkjenne sin egen kompetanse.
        /// </summary>
        /// <param name="competencyOwnerUserId">Brukeren brukerkompetansen tilhører.</param>
        /// <param name="isApprover">Om innlogget bruker er aktiv godkjenner for kompetansen.</param>
        public static bool CanApprove(Actor actor, int competencyOwnerUserId, bool isApprover)
            => actor.IsAdmin || (isApprover && competencyOwnerUserId != actor.UserId);
    }
}
