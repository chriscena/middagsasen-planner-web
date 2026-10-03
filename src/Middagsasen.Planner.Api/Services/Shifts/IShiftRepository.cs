using Middagsasen.Planner.Api.Data;

namespace Middagsasen.Planner.Api.Services.Shifts
{
    public interface IShiftRepository
    {
        /// <summary>
        /// Kjører <paramref name="work"/> i én transaksjon som holder en eksklusiv lås på ressursraden til commit,
        /// slik at samtidige endringer på samme ressurs serialiseres (kapasitet og duplikat sjekkes mot ferske data).
        /// Committer når <paramref name="work"/> fullfører, og ruller tilbake hvis den kaster.
        /// </summary>
        /// <exception cref="EntityNotFoundException">Ressursen finnes ikke.</exception>
        Task<T> InResourceLock<T>(int resourceId, Func<Task<T>> work);

        /// <summary>
        /// Henter ressursen uten tracking med alt som trengs for <see cref="ResourceMapper"/> og <see cref="ShiftRules"/>.
        /// Inne i <see cref="InResourceLock{T}"/> gir den ferske data.
        /// </summary>
        Task<EventResource?> GetResource(int resourceId);

        /// <summary>Henter vakta med tracking (for endring/sletting).</summary>
        Task<EventResourceUser?> GetShift(int shiftId);

        /// <summary>Henter ressurs-id-en til vakta, eller <c>null</c> hvis vakta ikke finnes.</summary>
        Task<int?> GetResourceIdForShift(int shiftId);

        Task<bool> UserExists(int userId);

        /// <summary>Henter brukerens opplæring på ressurstypen med tracking, eller <c>null</c>.</summary>
        Task<ResourceTypeTraining?> GetTraining(int userId, int resourceTypeId);

        /// <summary>Henter opplæringen uten tracking, med ressurstype og bekreftet av (for svar).</summary>
        Task<ResourceTypeTraining> GetTrainingForResponse(int trainingId);

        /// <summary>Ressurstypene brukeren har en opplæringsrad for (uansett status).</summary>
        Task<IReadOnlyList<int>> GetTrainingResourceTypeIds(int userId);

        /// <summary>Setter minimum bemanning på ressursen direkte i databasen (uten SaveChanges).</summary>
        Task SetMinimumStaff(int resourceId, int minimumStaff);

        void AddShift(EventResourceUser shift);
        void RemoveShift(EventResourceUser shift);
        void AddTraining(ResourceTypeTraining training);

        /// <summary>
        /// Forkaster alt konteksten sporer (også endringer som ikke ble lagret), slik at en operasjon kan prøves på nytt
        /// fra ren tilstand etter <see cref="TrainingConflictException"/>.
        /// </summary>
        void DiscardChanges();

        /// <summary>Lagrer endringer.</summary>
        /// <exception cref="DomainValidationException">
        /// Brukeren står allerede på ressursen (unik indeks, samtidig påmelding).
        /// </exception>
        /// <exception cref="TrainingConflictException">Opplæringsraden ble opprettet av en samtidig forespørsel (unik indeks).</exception>
        Task SaveChangesAsync();
    }
}
