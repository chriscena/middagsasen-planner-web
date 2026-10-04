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
    /// Rekkefølge for å unngå vranglås når flere rader låses i samme transaksjon: vaktlista før ressursene, og ressursene
    /// i stigende id-rekkefølge (<see cref="LockResources"/>).
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

        /// <summary>Låser ressursraden.</summary>
        /// <returns><c>false</c> hvis ressursen ikke finnes.</returns>
        public static async Task<bool> LockResource(this PlannerDbContext dbContext, int resourceId)
        {
            var locked = await dbContext.EventResource
                .Where(r => r.EventResourceId == resourceId)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.MinimumStaff, r => r.MinimumStaff));
            return locked > 0;
        }

        /// <summary>
        /// Låser ressursradene til vaktlista med de gitte id-ene, én og én i stigende id-rekkefølge, slik at to transaksjoner
        /// som låser overlappende ressurser, alltid tar låsene i samme rekkefølge (én UPDATE med <c>IN</c> gir ingen
        /// garanti for rekkefølgen). Id-er som ikke finnes eller tilhører en annen vaktliste, hoppes over.
        /// </summary>
        /// <returns>Id-ene som ble låst.</returns>
        public static async Task<IReadOnlyList<int>> LockResources(this PlannerDbContext dbContext, int eventId, IEnumerable<int> resourceIds)
        {
            var locked = new List<int>();
            foreach (var resourceId in resourceIds.Distinct().Order())
            {
                var count = await dbContext.EventResource
                    .Where(r => r.EventResourceId == resourceId && r.EventId == eventId)
                    .ExecuteUpdateAsync(s => s.SetProperty(r => r.MinimumStaff, r => r.MinimumStaff));
                if (count > 0)
                    locked.Add(resourceId);
            }
            return locked;
        }
    }
}
