namespace Middagsasen.Planner.Api.Services.Shifts
{
    /// <summary>Ta vakt på en ressurs (<c>POST api/resources/{id}/shifts</c>).</summary>
    public class SignUpRequest
    {
        /// <summary>Brukeren som settes opp. <c>null</c> = innlogget bruker. En annen bruker kan bare settes opp av admin.</summary>
        public int? UserId { get; set; }

        /// <summary>Vaktens start. <c>null</c> = ressursens start. Må ligge innenfor ressursens tider.</summary>
        public DateTime? StartTime { get; set; }

        /// <summary>Vaktens slutt. <c>null</c> = ressursens slutt. Må ligge innenfor ressursens tider.</summary>
        public DateTime? EndTime { get; set; }

        public string? Comment { get; set; }

        /// <summary>
        /// Opplæringen til brukeren som settes opp, lagret i samme transaksjon som vakta. Samme betydning som
        /// <see cref="SetTrainingRequest.TrainingCompleted"/>: <c>true</c> = gjennomført / trengs ikke (bekreftes av innlogget
        /// bruker), <c>false</c> = ønsker opplæring (trenerne varsles på SMS).
        /// Påkrevd når ressurstypen har opplæring og brukeren ikke har svart før (ressursens <c>mustAnswerTraining</c>); da gir
        /// <c>null</c> 400. Har brukeren svart før, endrer <c>null</c> eller samme verdi ingenting, mens en annen verdi oppdaterer
        /// opplæringen (trenerne varsles bare ved overgang til <c>false</c>). Ignoreres når ressurstypen ikke har opplæring.
        /// </summary>
        public bool? TrainingCompleted { get; set; }
    }
}
