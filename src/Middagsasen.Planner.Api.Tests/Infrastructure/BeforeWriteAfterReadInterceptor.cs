using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Middagsasen.Planner.Api.Tests.Infrastructure
{
    /// <summary>
    /// Kjører <paramref name="beforeWrite"/> én gang, rett før første skrivekommando (non-query, f.eks.
    /// <c>ExecuteUpdate</c>) som kommer etter en lesing. Altså mellom «les» og «skriv» i en les-endre-skriv-operasjon,
    /// også når den skrives med <c>ExecuteUpdate</c> uten <c>SaveChanges</c> (som <see cref="BeforeSaveInterceptor"/> ikke ser).
    /// Skrivekommandoer før første lesing (som ressurslåsen, en UPDATE) hoppes over.
    /// </summary>
    public sealed class BeforeWriteAfterReadInterceptor(Func<Task> beforeWrite) : DbCommandInterceptor
    {
        private bool _hasRead;
        private bool _hasRun;

        public override ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            _hasRead = true;
            return ValueTask.FromResult(result);
        }

        public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
        {
            _hasRead = true;
            return result;
        }

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (_hasRead && !_hasRun)
            {
                _hasRun = true;
                await beforeWrite();
            }
            return result;
        }
    }
}
