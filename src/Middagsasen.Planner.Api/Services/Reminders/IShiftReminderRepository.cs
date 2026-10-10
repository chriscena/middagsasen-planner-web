using Middagsasen.Planner.Api.Data;

namespace Middagsasen.Planner.Api.Services.Reminders
{
    /// <summary>En bruker som skal ha vaktpåminnelse for en vaktdag, med vaktene den gjelder.</summary>
    /// <param name="UserId">Brukeren.</param>
    /// <param name="UserName">
    /// Lagret brukernavn. Normalt et normalisert telefonnummer (8 sifre), men eldre brukere kan ha et brukernavn som
    /// ikke er et nummer (f.eks. «admin»); servicen må sjekke med <see cref="Users.UserNameExtensions.ToNormalizedUserName"/>.
    /// </param>
    /// <param name="FirstName">Fornavn til hilsenen, kan mangle.</param>
    /// <param name="Shifts">Brukerens vakter på vaktdagen, med effektive tider.</param>
    /// <param name="FailedReminder">
    /// Loggraden fra et tidligere forsøk som ikke lyktes (<c>Success = false</c>), med tracking så den kan oppdateres,
    /// eller <c>null</c> når det ikke er forsøkt før.
    /// </param>
    public sealed record ReminderCandidate(int UserId, string UserName, string? FirstName, IReadOnlyList<ReminderShift> Shifts, ShiftReminder? FailedReminder);

    public interface IShiftReminderRepository
    {
        /// <summary>
        /// Brukerne som skal ha påminnelse for <paramref name="shiftDate"/>: aktive brukere med påminnelse på, som har
        /// minst én vakt med effektiv start på dagen (<see cref="ShiftReminderRules.EffectivePeriod"/>), og som ikke
        /// allerede har en vellykket påminnelse for dagen.
        /// </summary>
        Task<IReadOnlyList<ReminderCandidate>> GetCandidates(DateOnly shiftDate, CancellationToken cancellationToken);

        /// <summary>Legger til en ny loggrad (lagres av <see cref="SaveChangesAsync"/>).</summary>
        void Add(ShiftReminder reminder);

        /// <summary>
        /// Lagrer nye og oppdaterte loggrader. Et brudd på den unike indeksen (samtidig kjøring som allerede har
        /// en rad for samme bruker og dag) kastes videre som <see cref="Microsoft.EntityFrameworkCore.DbUpdateException"/>.
        /// </summary>
        Task SaveChangesAsync(CancellationToken cancellationToken);
    }
}
