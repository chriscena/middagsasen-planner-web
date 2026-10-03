namespace Middagsasen.Planner.Api.Services.Shifts
{
    /// <summary>Endre en vakt (<c>PUT api/shifts/{id}</c>).</summary>
    public class ChangeShiftRequest
    {
        /// <summary>Flytt vakta til en annen bruker. <c>null</c> = behold eieren. En annen bruker kan bare settes av admin.</summary>
        public int? UserId { get; set; }

        /// <summary>Ny start. <c>null</c> = behold lagret start. Må ligge innenfor ressursens tider.</summary>
        public DateTime? StartTime { get; set; }

        /// <summary>Ny slutt. <c>null</c> = behold lagret slutt. Må ligge innenfor ressursens tider.</summary>
        public DateTime? EndTime { get; set; }

        /// <summary>Kommentaren. Settes alltid: <c>null</c> eller tom fjerner kommentaren (klienten sender hele vakta).</summary>
        public string? Comment { get; set; }

        /// <summary>
        /// Opplæringen til eieren av vakta etter endringen (den nye eieren hvis <see cref="UserId"/> flytter vakta), lagret i
        /// samme transaksjon som vakta. Samme betydning som <see cref="SetTrainingRequest.TrainingCompleted"/>: <c>true</c> =
        /// gjennomført / trengs ikke (bekreftes av innlogget bruker), <c>false</c> = ønsker opplæring (trenerne varsles på SMS).
        /// Påkrevd bare når vakta flyttes til en bruker som ikke har svart før på en ressurstype med opplæring; da gir
        /// <c>null</c> 400. Ellers er det valgfritt: uten opplæringsrad opprettes raden hvis svaret sendes; med rad endrer
        /// <c>null</c> eller samme verdi ingenting, mens en annen verdi oppdaterer opplæringen (trenerne varsles bare ved
        /// overgang til <c>false</c>). Endepunktet krever admin eller eieren av vakta, så bare de kan endre opplæringen her;
        /// trenere får 403 og bruker <c>PUT api/shifts/{id}/training</c>.
        /// Ignoreres når ressurstypen ikke har opplæring.
        /// </summary>
        public bool? TrainingCompleted { get; set; }
    }
}
