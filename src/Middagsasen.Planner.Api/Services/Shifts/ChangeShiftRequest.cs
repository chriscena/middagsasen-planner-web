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
    }
}
