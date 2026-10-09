using Microsoft.EntityFrameworkCore;

namespace Middagsasen.Planner.Api.Data
{
    /// <summary>
    /// Eksklusive radlåser som holdes til transaksjonen committes eller rulles tilbake. Må kalles inne i en transaksjon.
    /// <para>
    /// En rad låses ved å oppdatere en kolonne til sin egen verdi. En UPDATE tar eksklusiv radlås som holdes til commit
    /// både i SQL Server og PostgreSQL, uten databasespesifikk SQL (som UPDLOCK-hint eller <c>SELECT ... FOR UPDATE</c>).
    /// En samtidig transaksjon som låser samme rad, venter derfor til den første er ferdig, og leser deretter ferske data.
    /// </para>
    /// <para>
    /// Vranglås: den som låser flere rader, tar vaktlista før oppgavene (<see cref="LockEvent"/>, så
    /// <see cref="LockEventResources"/>). Den som låser én oppgave (<see cref="LockResource"/>, påmelding og ledige vakter),
    /// venter ikke på vaktlisteraden eller andre oppgaver mens den holder låsen, og kan derfor ikke inngå i en sykel med
    /// den som låser flere: den som låser flere, venter i verste fall til enkeltlåsen er committet.
    /// </para>
    /// </summary>
    public static class RowLocks
    {
        /// <summary>Låser raden til vaktlista.</summary>
        /// <returns><c>false</c> hvis vaktlista ikke finnes.</returns>
        public static async Task<bool> LockEvent(this PlannerDbContext dbContext, int eventId)
        {
            var locked = await dbContext.Events
                .Where(e => e.EventId == eventId)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.Name, e => e.Name));
            return locked > 0;
        }

        /// <summary>Låser oppgaveraden.</summary>
        /// <returns><c>false</c> hvis oppgaven ikke finnes.</returns>
        public static async Task<bool> LockResource(this PlannerDbContext dbContext, int resourceId)
            => await LockResourceRows(dbContext.EventResource.Where(r => r.EventResourceId == resourceId)) > 0;

        /// <summary>
        /// Låser alle oppgaveradene til vaktlista med én UPDATE. Kall <see cref="LockEvent"/> først (se vranglås over).
        /// </summary>
        public static Task LockEventResources(this PlannerDbContext dbContext, int eventId)
            => LockResourceRows(dbContext.EventResource.Where(r => r.EventId == eventId));

        private static Task<int> LockResourceRows(IQueryable<EventResource> resources)
            => resources.ExecuteUpdateAsync(s => s.SetProperty(r => r.ShiftCount, r => r.ShiftCount));
    }
}
