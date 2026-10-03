namespace Middagsasen.Planner.Api.Services
{
    /// <summary>
    /// Kastes når operasjonen krever innlogget bruker, men ingen er innlogget. Mappes til 401 Unauthorized.
    /// Brukes i stedet for <see cref="UnauthorizedAccessException"/>, som også kastes av vanlige I/O-feil.
    /// </summary>
    [Serializable]
    public class NotAuthenticatedException : Exception
    {
        /// <summary>Norsk standardmelding som vises til brukeren når ingen annen melding er gitt.</summary>
        public const string DefaultMessage = "Du må være innlogget.";

        public NotAuthenticatedException() : base(DefaultMessage)
        {
        }

        public NotAuthenticatedException(string? message) : base(message ?? DefaultMessage)
        {
        }

        public NotAuthenticatedException(string? message, Exception? innerException) : base(message ?? DefaultMessage, innerException)
        {
        }
    }
}
