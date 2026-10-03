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
        /// Svaret på «trenger du opplæring?». Påkrevd når ressurstypen har opplæring og brukeren ikke har svart
        /// før (ressursens <c>mustAnswerTraining</c>); da gir <c>null</c> 400. <c>true</c> registrerer at brukeren
        /// ønsker opplæring og varsler trenerne på SMS, <c>false</c> registrerer at brukeren ikke trenger opplæring.
        /// Ignoreres når brukeren allerede har svart, eller ressurstypen ikke har opplæring.
        /// </summary>
        public bool? NeedsTraining { get; set; }
    }
}
