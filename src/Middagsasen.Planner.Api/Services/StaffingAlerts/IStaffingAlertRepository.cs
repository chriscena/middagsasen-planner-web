using Middagsasen.Planner.Api.Core;
using Middagsasen.Planner.Api.Services.Shifts;
using Middagsasen.Planner.Api.Services.SmsSender;

namespace Middagsasen.Planner.Api.Services.StaffingAlerts
{
    /// <summary>Oppgaven slik bemanningsvarselet trenger den, lest med én spørring.</summary>
    /// <param name="ResourceTypeName">Vakttypen, f.eks. «storheis».</param>
    /// <param name="EventName">Vaktlistens navn, f.eks. «Åpningstid» eller «Diskokveld».</param>
    /// <param name="StartTime">Oppgavens start, norsk lokal tid.</param>
    /// <param name="EndTime">Oppgavens slutt, norsk lokal tid.</param>
    /// <param name="Staffing">Bemanningen (antall vakter og bemannede vakter), det <see cref="StaffingAlertRules.IsDue"/> vurderer.</param>
    public sealed record TaskStaffing(string ResourceTypeName, string EventName, DateTime StartTime, DateTime EndTime, ResourceStaffing Staffing);

    /// <summary>
    /// Oppslagene <see cref="StaffingAlertNotifier"/> trenger. Alt hentes uten tracking, og brukere projiseres til det
    /// SMS-en trenger (navn og telefonnummer) i stedet for hele <see cref="Data.User"/>. Går ikke via
    /// <see cref="Users.IUserService.GetUserById"/>, som laster opplæringene med vakttyper og bekreftere for et svar
    /// vi bare skulle lese navnet fra.
    /// </summary>
    public interface IStaffingAlertRepository
    {
        /// <summary>Navnet til brukeren som trakk seg.</summary>
        /// <exception cref="InvalidOperationException">Brukeren finnes ikke.</exception>
        Task<PersonName> GetName(int userId);

        /// <summary>Oppgaven med ferske tall for bemanningen, eller <c>null</c> hvis oppgaven er borte.</summary>
        Task<TaskStaffing?> GetTask(int resourceId);

        /// <summary>
        /// Mottakerne av bemanningsvarsel: aktive admin som har slått det på, unntatt <paramref name="excludeUserId"/>
        /// (den som trakk seg).
        /// </summary>
        Task<IReadOnlyList<SmsRecipient>> GetRecipients(int excludeUserId);
    }
}
