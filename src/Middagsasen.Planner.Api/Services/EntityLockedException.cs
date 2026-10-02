namespace Middagsasen.Planner.Api.Services
{
    /// <summary>
    /// Kastes når en entitet er i en tilstand som ikke tillater operasjonen
    /// (f.eks. en behandlet timeføring). Mappes til 409 Conflict.
    /// </summary>
    [Serializable]
    public class EntityLockedException : Exception
    {
        /// <summary>Norsk standardmelding som vises til brukeren når ingen annen melding er gitt.</summary>
        public const string DefaultMessage = "Dette er låst og kan ikke endres.";

        public EntityLockedException() : base(DefaultMessage)
        {
        }

        public EntityLockedException(string? message) : base(message ?? DefaultMessage)
        {
        }

        public EntityLockedException(string? message, Exception? innerException) : base(message ?? DefaultMessage, innerException)
        {
        }
    }
}
