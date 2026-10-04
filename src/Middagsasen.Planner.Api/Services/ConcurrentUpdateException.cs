namespace Middagsasen.Planner.Api.Services
{
    /// <summary>
    /// Kastes når noen andre har endret dataene siden klienten lastet dem, slik at endringen ikke kan lagres uten å
    /// overskrive deres (f.eks. antall vakter på en oppgave i vaktlisteskjemaet, #151). Mappes til 409 Conflict.
    /// Ikke det samme som <see cref="EntityLockedException"/>, som betyr at entiteten er i en tilstand som ikke kan endres.
    /// </summary>
    [Serializable]
    public class ConcurrentUpdateException : Exception
    {
        /// <summary>Norsk standardmelding som vises til brukeren når ingen annen melding er gitt.</summary>
        public const string DefaultMessage = "Dette er endret av noen andre. Last siden på nytt og prøv igjen.";

        public ConcurrentUpdateException() : base(DefaultMessage)
        {
        }

        public ConcurrentUpdateException(string? message) : base(message ?? DefaultMessage)
        {
        }

        public ConcurrentUpdateException(string? message, Exception? innerException) : base(message ?? DefaultMessage, innerException)
        {
        }
    }
}
