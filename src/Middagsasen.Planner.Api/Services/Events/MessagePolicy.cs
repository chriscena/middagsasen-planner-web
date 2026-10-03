using Middagsasen.Planner.Api.Data;

namespace Middagsasen.Planner.Api.Services.Events
{
    /// <summary>
    /// Tilgangsregler for beskjeder på vaktressurser. Ren og uten avhengigheter.
    /// </summary>
    public static class MessagePolicy
    {
        /// <summary>Slette en beskjed: admin eller den som skrev den.</summary>
        public static bool CanDelete(Actor actor, EventResourceMessage message)
            => actor.IsAdminOrSelf(message.CreatedBy);
    }
}
