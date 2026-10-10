namespace Middagsasen.Planner.Api.Data
{
    /// <summary>
    /// Én vaktpåminnelse (SMS dagen før) per bruker per vaktdag. Raden er både dedup (unik indeks på
    /// <c>UserId</c> + <c>ShiftDate</c>) og logg: en feilet sending står igjen med <c>Success = false</c>
    /// og oppdateres når sendingen lykkes ved et nytt forsøk.
    /// </summary>
    public class ShiftReminder
    {
        public int ShiftReminderId { get; set; }
        public int UserId { get; set; }
        /// <summary>Dagen vaktene gjelder (norsk lokal dato), altså dagen etter at påminnelsen sendes.</summary>
        public DateOnly ShiftDate { get; set; }
        /// <summary>Tidspunktet for siste sendeforsøk, i UTC.</summary>
        public DateTime SentTime { get; set; }
        public bool Success { get; set; }
        /// <summary>Feilinfo fra SMS-tjenesten ved feil, ellers <c>null</c>.</summary>
        public string? Info { get; set; }

        public User User { get; set; } = null!;
    }
}
