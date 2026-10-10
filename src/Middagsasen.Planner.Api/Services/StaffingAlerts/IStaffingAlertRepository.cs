using Middagsasen.Planner.Api.Data;

namespace Middagsasen.Planner.Api.Services.StaffingAlerts
{
    /// <summary>Bemanningen på en oppgave slik bemanningsvarselet trenger den, lest med én spørring.</summary>
    /// <param name="ResourceTypeName">Vakttypen, f.eks. «storheis».</param>
    /// <param name="EventName">Vaktlistens navn, f.eks. «Åpningstid» eller «Diskokveld».</param>
    /// <param name="StartTime">Oppgavens start, norsk lokal tid.</param>
    /// <param name="EndTime">Oppgavens slutt, norsk lokal tid.</param>
    /// <param name="ShiftCount">Antall vakter på oppgaven.</param>
    /// <param name="StaffedCount">Antall bemannede vakter.</param>
    public sealed record TaskStaffing(string ResourceTypeName, string EventName, DateTime StartTime, DateTime EndTime, int ShiftCount, int StaffedCount);

    /// <summary>Oppslagene <see cref="StaffingAlertNotifier"/> trenger. Alt hentes uten tracking.</summary>
    public interface IStaffingAlertRepository
    {
        /// <exception cref="InvalidOperationException">Brukeren finnes ikke.</exception>
        Task<User> GetUser(int userId);

        /// <summary>Oppgaven med ferske tall for bemanningen, eller <c>null</c> hvis oppgaven er borte.</summary>
        Task<TaskStaffing?> GetTask(int resourceId);

        /// <summary>
        /// Mottakerne av bemanningsvarsel: aktive admin som har slått det på, unntatt <paramref name="excludeUserId"/>
        /// (den som trakk seg).
        /// </summary>
        Task<IReadOnlyList<User>> GetRecipients(int excludeUserId);
    }
}
