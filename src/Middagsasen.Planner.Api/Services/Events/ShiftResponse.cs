namespace Middagsasen.Planner.Api.Services.Events
{
    /// <summary>
    /// En vakt. Flaggene gjelder innlogget bruker og beregnes av <see cref="Shifts.ShiftRules"/>.
    /// </summary>
    public class ShiftResponse
    {
        public int Id { get; set; }
        public int EventResourceId { get; set; }
        public ShiftUserResponse User { get; set; } = null!;
        /// <summary>Vaktens start, norsk lokal tid uten sone (<c>yyyy-MM-ddTHH:mm</c>).</summary>
        public string? StartTime { get; set; }

        /// <summary>Vaktens slutt, norsk lokal tid uten sone (<c>yyyy-MM-ddTHH:mm</c>).</summary>
        public string? EndTime { get; set; }
        public string? Comment { get; set; }

        /// <summary>Eieren har bedt om opplæring på ressursens ressurstype og ikke fått den bekreftet ennå.</summary>
        public bool NeedsTraining { get; set; }

        /// <summary>Vakta tilhører innlogget bruker.</summary>
        public bool IsMine { get; set; }

        /// <summary>Innlogget bruker kan endre vakta (<c>PUT api/shifts/{id}</c>).</summary>
        public bool CanEdit { get; set; }

        /// <summary>Innlogget bruker kan trekke seg fra / slette vakta (<c>DELETE api/shifts/{id}</c>).</summary>
        public bool CanWithdraw { get; set; }

        /// <summary>
        /// Innlogget bruker (trener for ressurstypen eller admin) kan bekrefte at eieren har fått opplæring
        /// (<c>PUT api/shifts/{id}/training</c> med <c>trainingCompleted: true</c>).
        /// </summary>
        public bool CanConfirmTraining { get; set; }
    }
}
