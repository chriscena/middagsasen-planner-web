using Middagsasen.Planner.Api.Data;

namespace Middagsasen.Planner.Api.Services.Attachments
{
    /// <summary>
    /// Tilgangsregler for vedlegg og instrukser. Ren og uten avhengigheter: vurderes ut fra <see cref="Actor"/> og det
    /// lagrede vedlegget. Kvittering gjøres alltid for innlogget bruker selv, så den har ingen aktør-regel.
    /// </summary>
    public static class AttachmentPolicy
    {
        /// <summary>Laste opp, endre metadata og arkivere vedlegg, og lese arkiverte: admin.</summary>
        public static bool CanManage(Actor actor) => actor.IsAdmin;

        /// <summary>Laste ned et vedlegg: alle innloggede for aktive vedlegg, bare admin for arkiverte.</summary>
        public static bool CanDownload(Actor actor, bool isArchived) => !isArchived || actor.IsAdmin;

        /// <summary>Se kvitteringsrapporten (andres kvitteringsstatus): admin.</summary>
        public static bool CanViewReport(Actor actor) => actor.IsAdmin;

        /// <summary>Vedlegget kan kvitteres: det er en instruks og ikke arkivert.</summary>
        public static bool CanAcknowledge(ResourceTypeFile file) => file.RequiresAcknowledgement && file.Archived == null;
    }
}
