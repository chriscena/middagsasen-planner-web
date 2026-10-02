namespace Middagsasen.Planner.Api.Services
{
    /// <summary>
    /// Kastes ved bevisste valideringsfeil i domenet (ugyldig input). Mappes til 400 Bad Request,
    /// og meldingen vises til brukeren — den skal derfor være norsk og uten interne detaljer.
    /// </summary>
    /// <remarks>
    /// Bruk denne i stedet for <see cref="InvalidOperationException"/>, som behandles som en intern feil (500).
    /// </remarks>
    [Serializable]
    public class DomainValidationException : Exception
    {
        /// <summary>Norsk standardmelding som vises til brukeren når ingen annen melding er gitt.</summary>
        public const string DefaultMessage = "Forespørselen er ugyldig.";

        public DomainValidationException() : base(DefaultMessage)
        {
        }

        public DomainValidationException(string? message) : base(message ?? DefaultMessage)
        {
        }

        public DomainValidationException(string? message, Exception? innerException) : base(message ?? DefaultMessage, innerException)
        {
        }
    }
}
