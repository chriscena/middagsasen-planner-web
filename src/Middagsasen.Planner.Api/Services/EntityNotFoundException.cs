namespace Middagsasen.Planner.Api.Services
{
    /// <summary>
    /// Kastes når en entitet ikke finnes. Mappes til 404 Not Found.
    /// </summary>
    [Serializable]
    internal class EntityNotFoundException : Exception
    {
        /// <summary>Norsk standardmelding som vises til brukeren når ingen annen melding er gitt.</summary>
        public const string DefaultMessage = "Fant ikke det du lette etter.";

        public EntityNotFoundException() : base(DefaultMessage)
        {
        }

        public EntityNotFoundException(string? message) : base(message ?? DefaultMessage)
        {
        }

        public EntityNotFoundException(string? message, Exception? innerException) : base(message ?? DefaultMessage, innerException)
        {
        }
    }
}
