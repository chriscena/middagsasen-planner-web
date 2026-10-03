namespace Middagsasen.Planner.Api.Services.Shifts
{
    /// <summary>
    /// En opplæringsrad for samme (bruker, ressurstype) ble opprettet av en samtidig forespørsel mellom sjekken og
    /// lagringen (unik indeks <see cref="ShiftRepository.UniqueTrainingIndexName"/>). Låsen i
    /// <see cref="IShiftRepository.InResourceLock{T}"/> gjelder én ressurs, mens opplæringen gjelder alle ressurser av
    /// samme ressurstype, så dette kan skje ved påmelding på to ressurser samtidig. <see cref="ShiftService"/> prøver
    /// hele operasjonen én gang til; da finnes raden, og svaret behandles som for en eksisterende rad (står urørt ved
    /// <c>null</c> eller likt svar, oppdateres ved ulikt svar).
    /// </summary>
    public sealed class TrainingConflictException(Exception innerException)
        : Exception("Opplæringen ble opprettet av en samtidig forespørsel.", innerException);
}
