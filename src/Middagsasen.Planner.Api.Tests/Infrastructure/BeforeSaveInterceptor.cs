using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Middagsasen.Planner.Api.Tests.Infrastructure
{
    /// <summary>
    /// Kjører <paramref name="beforeSave"/> én gang, rett før første lagring. Brukes til å simulere at en
    /// parallell forespørsel skriver til databasen mellom en sjekk og lagringen.
    /// </summary>
    public sealed class BeforeSaveInterceptor(Func<Task> beforeSave) : SaveChangesInterceptor
    {
        private bool _hasRun;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!_hasRun)
            {
                _hasRun = true;
                await beforeSave();
            }
            return result;
        }
    }
}
