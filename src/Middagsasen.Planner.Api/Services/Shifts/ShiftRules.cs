namespace Middagsasen.Planner.Api.Services.Shifts
{
    /// <summary>Fakta om en vakt (en bruker på en ressurs), slik <see cref="ShiftRules"/> ser den.</summary>
    /// <param name="ShiftId">Id til vakta (EventResourceUserId).</param>
    /// <param name="UserId">Eieren av vakta.</param>
    /// <param name="NeedsTraining">Om eieren har bedt om opplæring på ressursens ressurstype (TrainingComplete = false).</param>
    public sealed record ShiftFacts(int ShiftId, int UserId, bool NeedsTraining);

    /// <summary>Fakta om en ressurs og vaktene på den, slik <see cref="ShiftRules"/> ser den.</summary>
    /// <param name="StartTime">Ressursens start, norsk lokal tid.</param>
    /// <param name="EndTime">Ressursens slutt, norsk lokal tid.</param>
    /// <param name="HasTraining">Om ressurstypen har opplæring (har trenere).</param>
    /// <param name="TrainerUserIds">Trenerne for ressurstypen.</param>
    /// <param name="Shifts">Vaktene på ressursen.</param>
    public sealed record ResourceFacts(
        int ResourceId,
        int ResourceTypeId,
        DateTime StartTime,
        DateTime EndTime,
        int MinimumStaff,
        bool HasTraining,
        IReadOnlyCollection<int> TrainerUserIds,
        IReadOnlyList<ShiftFacts> Shifts)
    {
        /// <summary>Bemanningen (minimum bemanning og antall vakter), det <see cref="ShiftRules.IsMissingStaff(ResourceStaffing)"/> vurderer.</summary>
        public ResourceStaffing Staffing => new(MinimumStaff, Shifts.Count);
    }

    /// <summary>
    /// Bemanningen på en ressurs: minimum bemanning og antall vakter. Nok til reglene for ledige plasser, og kan leses
    /// med en lett spørring (<see cref="IShiftRepository.GetStaffing"/>) i stedet for hele <see cref="ResourceFacts"/>.
    /// </summary>
    public sealed record ResourceStaffing(int MinimumStaff, int ShiftCount);

    /// <summary>Hvorfor en handling på en vakt ble avvist.</summary>
    public enum ShiftRuleViolation
    {
        /// <summary>Brukeren har ikke tilgang (403).</summary>
        Forbidden,
        /// <summary>Ressursen er avsluttet, og bare admin kan endre den.</summary>
        Past,
        /// <summary>Ressursen er full (antall vakter &gt;= MinimumStaff), og bare admin kan overbooke.</summary>
        Full,
        /// <summary>Brukeren står allerede på ressursen.</summary>
        Duplicate,
        /// <summary>Vaktens tider ligger utenfor ressursens tider, eller start er etter slutt.</summary>
        InvalidTimes,
    }

    /// <summary>
    /// Regler for vakter. Ren og uten avhengigheter: alt som krever databaseoppslag (vaktene på ressursen,
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
    /// men ikke flytte vakta til en annen bruker, ta vakt på en full ressurs eller gjøre noe på en avsluttet ressurs.</item>
    /// <item>Opplæring (<see cref="CheckSetTraining"/>) kan settes av eieren, en trener for ressurstypen eller admin.</item>
    /// <item>Samme bruker kan aldri stå to ganger på samme ressurs, heller ikke når admin setter opp.</item>
    /// <item>Vaktens tider må ligge innenfor ressursens tider.</item>
    /// </list>
    /// </para>
    /// Alle tider (ressursens tider, vaktens tider og <c>now</c>) er norsk lokal tid uten tidssone.
    /// </summary>
    public static class ShiftRules
    {
        // --- Ressursstatus ---

        /// <summary>
        /// Ressursen mangler folk: færre vakter enn <c>MinimumStaff</c>.
        /// Samme formel som viewet <c>EventStatuses</c> (Middagsasen.Planner.Database/Views/EventStatuses.sql),
        /// som gir kalendermarkørene. Endres den ene, må den andre endres også.
        /// </summary>
        public static bool IsMissingStaff(ResourceStaffing staffing) => staffing.ShiftCount < staffing.MinimumStaff;

        /// <inheritdoc cref="IsMissingStaff(ResourceStaffing)"/>
        public static bool IsMissingStaff(ResourceFacts resource) => IsMissingStaff(resource.Staffing);

        /// <summary>Ressursen er full: minst <c>MinimumStaff</c> vakter. Det motsatte av <see cref="IsMissingStaff(ResourceFacts)"/>.</summary>
        public static bool IsFull(ResourceFacts resource) => !IsMissingStaff(resource);

        /// <summary>Ressursen er avsluttet: slutttiden er nådd.</summary>
        public static bool IsPast(ResourceFacts resource, DateTime now) => resource.EndTime <= now;

        /// <summary>Om brukeren er trener for ressursens ressurstype.</summary>
        public static bool IsTrainer(Actor actor, ResourceFacts resource) => resource.TrainerUserIds.Contains(actor.UserId);

        /// <summary>
        /// Brukeren må svare på «trenger du opplæring?» før hen kan settes opp: ressurstypen har opplæring,
        /// og brukeren har ingen opplæringsrad (verken ønsket eller fullført) for ressurstypen.
        /// </summary>
        /// <param name="userHasTraining">Om brukeren har en opplæringsrad for ressursens ressurstype.</param>
        public static bool MustAnswerTraining(ResourceFacts resource, bool userHasTraining)
            => resource.HasTraining && !userHasTraining;

        // --- Ta vakt ---

        /// <summary>
        /// Ta vakt / sette opp en bruker på ressursen. Rekkefølge: tilgang, fortid, duplikat, kapasitet, tider.
        /// </summary>
        /// <param name="targetUserId">Brukeren som settes opp.</param>
        /// <param name="startTime">Vaktens start, eller <c>null</c> for ressursens start.</param>
        /// <param name="endTime">Vaktens slutt, eller <c>null</c> for ressursens slutt.</param>
        public static ShiftRuleViolation? CheckSignUp(Actor actor, ResourceFacts resource, DateTime now, int targetUserId, DateTime? startTime = null, DateTime? endTime = null)
        {
            if (!actor.IsAdminOrSelf(targetUserId)) return ShiftRuleViolation.Forbidden;
            if (!actor.IsAdmin && IsPast(resource, now)) return ShiftRuleViolation.Past;
            if (resource.Shifts.Any(s => s.UserId == targetUserId)) return ShiftRuleViolation.Duplicate;
            if (!actor.IsAdmin && IsFull(resource)) return ShiftRuleViolation.Full;
            if (!AreTimesValid(resource, startTime, endTime)) return ShiftRuleViolation.InvalidTimes;
            return null;
        }

        /// <summary>Flagg: innlogget bruker kan ta vakt på ressursen nå (for seg selv, med ressursens tider).</summary>
        public static bool CanSignUp(Actor actor, ResourceFacts resource, DateTime now)
            => CheckSignUp(actor, resource, now, actor.UserId) is null;

        // --- Endre vakt ---

        /// <summary>
        /// Endre en vakt (tider, kommentar og for admin eier). Vurderes mot den lagrede vakta.
        /// <list type="bullet">
        /// <item>Admin kan endre alt, også flytte vakta til en annen bruker og endre i fortiden.</item>
        /// <item>Eieren kan endre tider og kommentar før ressursen er avsluttet, men ikke flytte vakta.</item>
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
            // ressursens tider er flyttet etter at vakta ble tatt.
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
        /// Sette opplæringen til eieren av vakta på ressursens ressurstype. Tilgang har eieren, en trener for
        /// ressurstypen og admin. Vanlige brukere og trenere kan ikke gjøre det på en avsluttet ressurs.
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
        /// ressurstypen eller admin, at eieren har bedt om opplæring, og at <see cref="CheckSetTraining"/> tillater det.
        /// </summary>
        public static bool CanConfirmTraining(Actor actor, ResourceFacts resource, DateTime now, ShiftFacts shift)
            => (actor.IsAdmin || IsTrainer(actor, resource))
                && shift.NeedsTraining
                && CheckSetTraining(actor, resource, now, shift) is null;

        // --- Trekke seg ---

        /// <summary>Trekke seg fra / slette en vakt: admin, eller eieren før ressursen er avsluttet.</summary>
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

        // --- Ledige plasser (kun admin) ---

        /// <summary>
        /// Ny <c>MinimumStaff</c> når admin legger til én ledig plass: én mer enn det største av <c>MinimumStaff</c> og
        /// antall vakter, slik at ressursen alltid får nøyaktig én ledig plass mer enn den har nå (også når den er overbooket).
        /// Vurderes mot ferske data under ressurslåsen, så samtidige klikk teller hver for seg.
        /// </summary>
        public static int MinimumStaffAfterAddingEmptySlot(ResourceStaffing staffing)
            => Math.Max(staffing.MinimumStaff, staffing.ShiftCount) + 1;

        /// <summary>
        /// Ny <c>MinimumStaff</c> når admin fjerner én ledig plass, eller <c>null</c> hvis ressursen ikke har noen ledig
        /// plass (full: minst like mange vakter som <c>MinimumStaff</c>, se <see cref="IsMissingStaff(ResourceStaffing)"/>).
        /// </summary>
        public static int? MinimumStaffAfterRemovingEmptySlot(ResourceStaffing staffing)
            => MinimumStaffAfterChange(staffing, -1);

        /// <summary>Antall ledige plasser: vakter som mangler før <c>MinimumStaff</c> er nådd (0 når ressursen er full eller overbooket).</summary>
        public static int EmptySlots(ResourceStaffing staffing) => Math.Max(0, staffing.MinimumStaff - staffing.ShiftCount);

        /// <summary>
        /// Ny <c>MinimumStaff</c> når admin endrer antall vakter relativt med <paramref name="change"/> (lagring av
        /// vaktlisteskjemaet, #151: endringen admin gjorde i skjemaet legges på den ferske verdien), eller <c>null</c>
        /// hvis det fjernes flere plasser enn det er ledige. Samme regel som å fjerne én og én ledig plass
        /// (<see cref="MinimumStaffAfterRemovingEmptySlot"/>): bemannede vakter fjernes aldri, og resultatet blir aldri
        /// under antall vakter eller under 0. Økning er alltid lov og legges på <c>MinimumStaff</c> som den er.
        /// </summary>
        public static int? MinimumStaffAfterChange(ResourceStaffing staffing, int change)
            => change >= -EmptySlots(staffing) ? staffing.MinimumStaff + change : null;

        // --- Tider ---

        /// <summary>
        /// Vaktens tider ligger innenfor ressursens tider, og start er ikke etter slutt. <c>null</c> betyr
        /// ressursens tid og er alltid gyldig.
        /// </summary>
        public static bool AreTimesValid(ResourceFacts resource, DateTime? startTime, DateTime? endTime)
        {
            var start = startTime ?? resource.StartTime;
            var end = endTime ?? resource.EndTime;
            return start >= resource.StartTime && end <= resource.EndTime && start <= end;
        }
    }
}
