using Middagsasen.Planner.Api.Services.ResourceTypes;

namespace Middagsasen.Planner.Api.Services.Events
{
    /// <summary>
    /// En ressurs med vakter. Flaggene gjelder innlogget bruker og beregnes av <see cref="Shifts.ShiftRules"/>.
    /// </summary>
    public class ResourceResponse
    {
        public int Id { get; set; }
        public ResourceTypeResponse ResourceType { get; set; } = null!;
        /// <summary>Ressursens start, norsk lokal tid uten sone (<c>yyyy-MM-ddTHH:mm</c>).</summary>
        public string StartTime { get; set; } = null!;
        /// <summary>Ressursens slutt, norsk lokal tid uten sone (<c>yyyy-MM-ddTHH:mm</c>).</summary>
        public string EndTime { get; set; } = null!;
        public int MinimumStaff { get; set; }
        public IEnumerable<ShiftResponse> Shifts { get; set; } = new List<ShiftResponse>();
        public int EventId { get; internal set; }
        public IEnumerable<MessageResponse> Messages { get; set; } = new List<MessageResponse>();

        /// <summary>Kompetansekrav som ikke er oppfylt av vaktene. Bare en advarsel; blokkerer ikke påmelding. Tom liste når alt er oppfylt.</summary>
        public IEnumerable<CompetencyWarningResponse> CompetencyWarnings { get; internal set; } = new List<CompetencyWarningResponse>();

        /// <summary>Færre vakter enn <see cref="MinimumStaff"/> (samme formel som kalenderens EventStatuses).</summary>
        public bool IsMissingStaff { get; internal set; }

        /// <summary>Minst <see cref="MinimumStaff"/> vakter. Vanlige brukere kan da ikke ta vakt; admin kan overbooke.</summary>
        public bool IsFull { get; internal set; }

        /// <summary>Ressursen er avsluttet (slutttiden er passert, norsk tid). Bare admin kan endre vakter.</summary>
        public bool IsPast { get; internal set; }

        /// <summary>Innlogget bruker kan ta vakt her nå (ikke allerede påmeldt; for ikke-admin også ikke full og ikke avsluttet).</summary>
        public bool CanSignUp { get; internal set; }

        /// <summary>
        /// Ressurstypen har opplæring og innlogget bruker har ikke svart på «trenger du opplæring?» for den.
        /// Da må <c>trainingCompleted</c> sendes med når brukeren tar vakt for seg selv.
        /// </summary>
        public bool MustAnswerTraining { get; internal set; }
    }
}
