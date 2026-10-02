namespace Middagsasen.Planner.Api.Services
{
    /// <summary>
    /// Kastes når innlogget bruker ikke har tilgang til operasjonen. Mappes til 403 Forbidden.
    /// </summary>
    [Serializable]
    public class ForbiddenAccessException : Exception
    {
        /// <summary>Norsk standardmelding som vises til brukeren når ingen annen melding er gitt.</summary>
        public const string DefaultMessage = "Du har ikke tilgang til å utføre denne handlingen.";

        public ForbiddenAccessException() : base(DefaultMessage)
        {
        }

        public ForbiddenAccessException(string? message) : base(message ?? DefaultMessage)
        {
        }

        public ForbiddenAccessException(string? message, Exception? innerException) : base(message ?? DefaultMessage, innerException)
        {
        }
    }
}
