namespace Middagsasen.Planner.Api.Services.Shifts
{
    /// <summary>Fakta om en vakt (en bruker på en oppgave), slik <see cref="ShiftRules"/> ser den.</summary>
    /// <param name="ShiftId">Id til vakta (EventResourceUserId).</param>
    /// <param name="UserId">Eieren av vakta.</param>
    /// <param name="NeedsTraining">Om eieren har bedt om opplæring på oppgavens vakttype (TrainingComplete = false).</param>
    public sealed record ShiftFacts(int ShiftId, int UserId, bool NeedsTraining);

    /// <summary>Fakta om en oppgave og vaktene på den, slik <see cref="ShiftRules"/> ser den.</summary>
    /// <param name="StartTime">Oppgavens start, norsk lokal tid.</param>
    /// <param name="EndTime">Oppgavens slutt, norsk lokal tid.</param>
    /// <param name="HasTraining">Om vakttypen har opplæring (har trenere).</param>
    /// <param name="TrainerUserIds">Trenerne for vakttypen.</param>
    /// <param name="Shifts">Vaktene på oppgaven.</param>
    public sealed record ResourceFacts(
        int ResourceId,
        int ResourceTypeId,
        DateTime StartTime,
        DateTime EndTime,
        int ShiftCount,
        bool HasTraining,
        IReadOnlyCollection<int> TrainerUserIds,
        IReadOnlyList<ShiftFacts> Shifts)
    {
        /// <summary>Bemanningen (antall vakter og bemannede vakter), det <see cref="ShiftRules.IsMissingStaff(ResourceStaffing)"/> vurderer.</summary>
        public ResourceStaffing Staffing => new(ShiftCount, Shifts.Count);
    }

    /// <summary>
    /// Bemanningen på en oppgave: antall vakter og bemannede vakter. Nok til reglene for ledige vakter, og kan leses
    /// med en lett spørring (<see cref="IShiftRepository.GetStaffing"/>) i stedet for hele <see cref="ResourceFacts"/>.
    /// </summary>
    public sealed record ResourceStaffing(int ShiftCount, int StaffedCount);

    /// <summary>Hvorfor en handling på en vakt ble avvist.</summary>
    public enum ShiftRuleViolation
    {
        /// <summary>Brukeren har ikke tilgang (403).</summary>
        Forbidden,
        /// <summary>Oppgaven er avsluttet, og bare admin kan endre den.</summary>
        Past,
        /// <summary>Oppgaven er full (bemannede vakter &gt;= ShiftCount), og bare admin kan overbooke.</summary>
        Full,
        /// <summary>Brukeren står allerede på oppgaven.</summary>
        Duplicate,
        /// <summary>Vaktens tider ligger utenfor oppgavens tider, eller start er etter slutt.</summary>
        InvalidTimes,
    }

    /// <summary>
    /// Regler for vakter. Ren og uten avhengigheter: alt som krever databaseoppslag (vaktene på oppgaven,
    /// trenerne, opplæringen) og «nå» slås opp av servicen og sendes inn.
    /// <para>
    /// Samme funksjoner brukes både til flaggene i svarene (f.eks. <c>CanSignUp</c>, <c>CanEdit</c>) og til
    /// håndhevelsen i <see cref="ShiftService"/>, slik at knappene i klienten og reglene ikke kan gli fra hverandre.
    /// En sjekk returnerer <c>null</c> når handlingen er lov, ellers hvorfor den avvises.
    /// </para>
    /// <para>
    /// Tilgang (fra #96, med endringene i #97):
    /// <list type="bullet">
    /// <item>Admin kan alt: sette opp hvem som helst, overbooke, flytte vakter og endre i fortiden.</item>
    /// <item>En vanlig bruker kan ta vakt for seg selv, endre egen vakt (tider, kommentar) og trekke seg,
    /// men ikke flytte vakta til en annen bruker, ta vakt på en full oppgave eller gjøre noe på en avsluttet oppgave.</item>
    /// <item>Opplæring (<see cref="CheckSetTraining"/>) kan settes av eieren, en trener for vakttypen eller admin.</item>
    /// <item>Samme bruker kan aldri stå to ganger på samme oppgave, heller ikke når admin setter opp.</item>
    /// <item>Vaktens tider må ligge innenfor oppgavens tider.</item>
    /// </list>
    /// </para>
    /// Alle tider (oppgavens tider, vaktens tider og <c>now</c>) er norsk lokal tid uten tidssone.
    /// </summary>
    public static class ShiftRules
    {
        // --- Oppgavestatus ---

        /// <summary>
        /// Oppgaven mangler folk: færre bemannede vakter enn <c>ShiftCount</c>.
        /// Samme formel som viewet <c>EventStatuses</c> (Middagsasen.Planner.Database/Views/EventStatuses.sql),
        /// som gir kalendermarkørene. Endres den ene, må den andre endres også.
        /// </summary>
        public static bool IsMissingStaff(ResourceStaffing staffing) => staffing.StaffedCount < staffing.ShiftCount;

        /// <inheritdoc cref="IsMissingStaff(ResourceStaffing)"/>
        public static bool IsMissingStaff(ResourceFacts resource) => IsMissingStaff(resource.Staffing);

        /// <summary>
        /// Antall ledige vakter: <c>ShiftCount</c> minus bemannede vakter, aldri under 0 (en overbooket oppgave har
        /// ingen ledige). Er over 0 nøyaktig når <see cref="IsMissingStaff(ResourceStaffing)"/> er sann.
        /// </summary>
        public static int OpenShifts(ResourceStaffing staffing) => Math.Max(0, staffing.ShiftCount - staffing.StaffedCount);

        /// <summary>Oppgaven er full: minst <c>ShiftCount</c> bemannede vakter. Det motsatte av <see cref="IsMissingStaff(ResourceFacts)"/>.</summary>
        public static bool IsFull(ResourceFacts resource) => !IsMissingStaff(resource);

        /// <summary>Oppgaven er avsluttet: slutttiden er nådd.</summary>
        public static bool IsPast(ResourceFacts resource, DateTime now) => resource.EndTime <= now;

        /// <summary>Om brukeren er trener for oppgavens vakttype.</summary>
        public static bool IsTrainer(Actor actor, ResourceFacts resource) => resource.TrainerUserIds.Contains(actor.UserId);

        /// <summary>
        /// Brukeren må svare på «trenger du opplæring?» før hen kan settes opp: vakttypen har opplæring,
        /// og brukeren har ingen opplæringsrad (verken ønsket eller fullført) for vakttypen.
        /// </summary>
        /// <param name="userHasTraining">Om brukeren har en opplæringsrad for oppgavens vakttype.</param>
        public static bool MustAnswerTraining(ResourceFacts resource, bool userHasTraining)
            => resource.HasTraining && !userHasTraining;

        // --- Ta vakt ---

        /// <summary>
        /// Ta vakt / sette opp en bruker på oppgaven. Rekkefølge: tilgang, fortid, duplikat, kapasitet, tider.
        /// </summary>
        /// <param name="targetUserId">Brukeren som settes opp.</param>
        /// <param name="startTime">Vaktens start, eller <c>null</c> for oppgavens start.</param>
        /// <param name="endTime">Vaktens slutt, eller <c>null</c> for oppgavens slutt.</param>
        public static ShiftRuleViolation? CheckSignUp(Actor actor, ResourceFacts resource, DateTime now, int targetUserId, DateTime? startTime = null, DateTime? endTime = null)
        {
            if (!actor.IsAdminOrSelf(targetUserId)) return ShiftRuleViolation.Forbidden;
            if (!actor.IsAdmin && IsPast(resource, now)) return ShiftRuleViolation.Past;
            if (resource.Shifts.Any(s => s.UserId == targetUserId)) return ShiftRuleViolation.Duplicate;
            if (!actor.IsAdmin && IsFull(resource)) return ShiftRuleViolation.Full;
            if (!AreTimesValid(resource, startTime, endTime)) return ShiftRuleViolation.InvalidTimes;
            return null;
        }

        /// <summary>Flagg: innlogget bruker kan ta vakt på oppgaven nå (for seg selv, med oppgavens tider).</summary>
        public static bool CanSignUp(Actor actor, ResourceFacts resource, DateTime now)
            => CheckSignUp(actor, resource, now, actor.UserId) is null;

        // --- Endre vakt ---

        /// <summary>
        /// Endre en vakt (tider, kommentar og for admin eier). Vurderes mot den lagrede vakta.
        /// <list type="bullet">
        /// <item>Admin kan endre alt, også flytte vakta til en annen bruker og endre i fortiden.</item>
        /// <item>Eieren kan endre tider og kommentar før oppgaven er avsluttet, men ikke flytte vakta.</item>
        /// <item>Alle andre, også trenere, får <see cref="ShiftRuleViolation.Forbidden"/>. Trenere bruker <see cref="CheckSetTraining"/>.</item>
        /// </list>
        /// </summary>
        /// <param name="newUserId">Brukeren vakta skal flyttes til, eller <c>null</c> for å beholde eieren.</param>
        /// <param name="startTime">Ny start, eller <c>null</c> for å beholde den lagrede.</param>
        /// <param name="endTime">Ny slutt, eller <c>null</c> for å beholde den lagrede.</param>
        /// <param name="currentStartTime">Vaktens lagrede start (brukes når bare slutt endres).</param>
        /// <param name="currentEndTime">Vaktens lagrede slutt (brukes når bare start endres).</param>
        public static ShiftRuleViolation? CheckChange(
            Actor actor,
            ResourceFacts resource,
            DateTime now,
            ShiftFacts shift,
            int? newUserId = null,
            DateTime? startTime = null,
            DateTime? endTime = null,
            DateTime? currentStartTime = null,
            DateTime? currentEndTime = null)
        {
            var movesShift = newUserId is { } id && id != shift.UserId;

            if (!actor.IsAdmin)
            {
                if (shift.UserId != actor.UserId) return ShiftRuleViolation.Forbidden;
                if (movesShift) return ShiftRuleViolation.Forbidden;
                if (IsPast(resource, now)) return ShiftRuleViolation.Past;
            }

            if (movesShift && resource.Shifts.Any(s => s.UserId == newUserId && s.ShiftId != shift.ShiftId))
                return ShiftRuleViolation.Duplicate;

            // Tidene valideres bare når de endres, slik at f.eks. en kommentar kan endres selv om
            // oppgavens tider er flyttet etter at vakta ble tatt.
            if ((startTime.HasValue || endTime.HasValue)
                && !AreTimesValid(resource, startTime ?? currentStartTime, endTime ?? currentEndTime))
                return ShiftRuleViolation.InvalidTimes;

            return null;
        }

        /// <summary>Flagg: innlogget bruker kan endre vakta (tider/kommentar).</summary>
        public static bool CanEdit(Actor actor, ResourceFacts resource, DateTime now, ShiftFacts shift)
            => CheckChange(actor, resource, now, shift) is null;

        // --- Opplæring ---

        /// <summary>
        /// Sette opplæringen til eieren av vakta på oppgavens vakttype. Tilgang har eieren, en trener for
        /// vakttypen og admin. Vanlige brukere og trenere kan ikke gjøre det på en avsluttet oppgave.
        /// <para>
        /// Eieren kan bevisst sette både fullført (selverklæringen «trenger ikke opplæring») og ønsker opplæring
        /// (varsler trenerne). Dette er en bevisst beslutning (issue #96) om å beholde dagens oppførsel, selv om en
        /// strengere regel der bare trener/admin kan bekrefte gjennomført opplæring ble vurdert.
        /// </para>
        /// </summary>
        public static ShiftRuleViolation? CheckSetTraining(Actor actor, ResourceFacts resource, DateTime now, ShiftFacts shift)
        {
            if (actor.IsAdmin) return null;
            if (shift.UserId != actor.UserId && !IsTrainer(actor, resource)) return ShiftRuleViolation.Forbidden;
            if (IsPast(resource, now)) return ShiftRuleViolation.Past;
            return null;
        }

        /// <summary>
        /// Flagg: innlogget bruker kan bekrefte at eieren har fått opplæring. Krever at brukeren er trener for
        /// vakttypen eller admin, at eieren har bedt om opplæring, og at <see cref="CheckSetTraining"/> tillater det.
        /// </summary>
        public static bool CanConfirmTraining(Actor actor, ResourceFacts resource, DateTime now, ShiftFacts shift)
            => (actor.IsAdmin || IsTrainer(actor, resource))
                && shift.NeedsTraining
                && CheckSetTraining(actor, resource, now, shift) is null;

        // --- Trekke seg ---

        /// <summary>Trekke seg fra / slette en vakt: admin, eller eieren før oppgaven er avsluttet.</summary>
        public static ShiftRuleViolation? CheckWithdraw(Actor actor, ResourceFacts resource, DateTime now, ShiftFacts shift)
        {
            if (!actor.IsAdmin)
            {
                if (shift.UserId != actor.UserId) return ShiftRuleViolation.Forbidden;
                if (IsPast(resource, now)) return ShiftRuleViolation.Past;
            }
            return null;
        }

        /// <summary>Flagg: innlogget bruker kan trekke seg fra / slette vakta.</summary>
        public static bool CanWithdraw(Actor actor, ResourceFacts resource, DateTime now, ShiftFacts shift)
            => CheckWithdraw(actor, resource, now, shift) is null;

        // --- Ledige vakter (kun admin) ---

        /// <summary>
        /// Ny <c>ShiftCount</c> når admin legger til én ledig vakt: én mer enn det største av <c>ShiftCount</c> og
        /// antall bemannede vakter, slik at oppgaven alltid får nøyaktig én ledig vakt mer enn den har nå (også når den er overbooket).
        /// Vurderes mot ferske data under oppgavelåsen, så samtidige klikk teller hver for seg.
        /// </summary>
        public static int ShiftCountAfterAddingEmptySlot(ResourceStaffing staffing)
            => Math.Max(staffing.ShiftCount, staffing.StaffedCount) + 1;

        /// <summary>
        /// Ny <c>ShiftCount</c> når admin fjerner én ledig vakt, eller <c>null</c> hvis oppgaven ikke har noen ledig
        /// vakt (full: minst like mange bemannede vakter som <c>ShiftCount</c>, se <see cref="IsMissingStaff(ResourceStaffing)"/>).
        /// </summary>
        public static int? ShiftCountAfterRemovingEmptySlot(ResourceStaffing staffing)
            => IsMissingStaff(staffing) ? staffing.ShiftCount - 1 : null;

        // --- Tider ---

        /// <summary>
        /// Vaktens tider ligger innenfor oppgavens tider, og start er ikke etter slutt. <c>null</c> betyr
        /// oppgavens tid og er alltid gyldig.
        /// </summary>
        public static bool AreTimesValid(ResourceFacts resource, DateTime? startTime, DateTime? endTime)
        {
            var start = startTime ?? resource.StartTime;
            var end = endTime ?? resource.EndTime;
            return start >= resource.StartTime && end <= resource.EndTime && start <= end;
        }
    }
}
