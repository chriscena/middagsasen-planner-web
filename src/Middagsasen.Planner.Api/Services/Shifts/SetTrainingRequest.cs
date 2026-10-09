namespace Middagsasen.Planner.Api.Services.Shifts
{
    /// <summary>
    /// Sette opplæringen til eieren av vakta på oppgavens vakttype (<c>PUT api/shifts/{id}/training</c>).
    /// </summary>
    public class SetTrainingRequest
    {
        /// <summary>
        /// <c>true</c> = opplæringen er gjennomført / trengs ikke (bekreftes av innlogget bruker).
        /// <c>false</c> = eieren ønsker opplæring; trenerne varsles på SMS.
        /// </summary>
        public bool TrainingCompleted { get; set; }
    }
}
