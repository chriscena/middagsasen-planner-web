using Middagsasen.Planner.Api.Data;

namespace Middagsasen.Planner.Api.Services.Shifts
{
    public interface IShiftRepository
    {
        /// <summary>
        /// Kjører <paramref name="work"/> i én transaksjon som holder en eksklusiv lås på oppgaveraden til commit,
        /// slik at samtidige endringer på samme oppgave serialiseres (kapasitet og duplikat sjekkes mot ferske data).
        /// Committer når <paramref name="work"/> fullfører, og ruller tilbake hvis den kaster.
        /// </summary>
        /// <exception cref="EntityNotFoundException">Oppgaven finnes ikke.</exception>
        Task<T> InResourceLock<T>(int resourceId, Func<Task<T>> work);

        /// <summary>
        /// Henter oppgaven uten tracking med det <see cref="ShiftService"/> og <see cref="ShiftRules"/> trenger
        /// (<see cref="ShiftFactsFactory.From"/>): <c>ResourceType</c> (navnet brukes i feilmeldinger), <c>ResourceType.Trainers</c>
        /// og <c>Shifts.User.Trainings</c>. Svaret til klienten leses separat via lesemodulen for oppgaver.
        /// Inne i <see cref="InResourceLock{T}"/> gir den ferske data.
        /// </summary>
        Task<EventResource?> GetResource(int resourceId);

        /// <summary>
        /// Leser bare bemanningen på oppgaven (<c>ShiftCount</c> og antall vakter) med én spørring. For endringer som
        /// ikke trenger hele grafen fra <see cref="GetResource"/>. Kalles inne i <see cref="InResourceLock{T}"/>, som gir
        /// ferske data og allerede har kastet <see cref="EntityNotFoundException"/> hvis oppgaven ikke finnes.
        /// </summary>
        /// <exception cref="InvalidOperationException">Oppgaven finnes ikke (kalt utenfor oppgavelåsen).</exception>
        Task<ResourceStaffing> GetStaffing(int resourceId);

        /// <summary>Henter vakta med tracking (for endring/sletting).</summary>
        Task<EventResourceUser?> GetShift(int shiftId);

        /// <summary>Henter oppgave-id-en til vakta, eller <c>null</c> hvis vakta ikke finnes.</summary>
        Task<int?> GetResourceIdForShift(int shiftId);

        Task<bool> UserExists(int userId);

        /// <summary>Henter brukerens opplæring på vakttypen med tracking, eller <c>null</c>.</summary>
        Task<ResourceTypeTraining?> GetTraining(int userId, int resourceTypeId);

        /// <summary>
        /// Setter antall vakter på oppgaven direkte i databasen (uten SaveChanges). Kalles inne i
        /// <see cref="InResourceLock{T}"/>, med en verdi regnet ut fra ferske data.
        /// </summary>
        Task SetShiftCount(int resourceId, int shiftCount);

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
        /// Brukeren står allerede på oppgaven (unik indeks, samtidig påmelding).
        /// </exception>
        /// <exception cref="TrainingConflictException">Opplæringsraden ble opprettet av en samtidig forespørsel (unik indeks).</exception>
        Task SaveChangesAsync();
    }
}
