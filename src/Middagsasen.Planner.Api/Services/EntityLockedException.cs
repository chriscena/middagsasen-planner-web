namespace Middagsasen.Planner.Api.Services
{
    /// <summary>
    /// Kastes når en entitet er i en tilstand som ikke tillater operasjonen
    /// (f.eks. en behandlet timeføring). Mappes til 409 Conflict.
    /// </summary>
    [Serializable]
    public class EntityLockedException : Exception
    {
        public EntityLockedException()
        {
        }

        public EntityLockedException(string? message) : base(message)
        {
        }

        public EntityLockedException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}
