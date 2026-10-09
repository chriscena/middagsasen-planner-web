namespace Middagsasen.Planner.Api.Services.Events
{
    /// <summary>
    /// Bestemmer hvilket døgn en vakt (ressurs) i en vaktliste havner på, gitt bare klokkeslett,
    /// og hvordan bemannede vakter følger ressursen når tidene endres.
    /// </summary>
    public static class ResourceTimes
    {
        private static readonly int[] DayOffsetsInPriorityOrder = [0, 1, -1];

        /// <summary>
        /// Vaktlistas slutt: hvis den er før start, ligger den neste døgn (legg til én dag). Ellers urørt.
        /// </summary>
        public static DateTime NormalizeEventEnd(DateTime eventStart, DateTime eventEnd) =>
            eventEnd < eventStart ? eventEnd.AddDays(1) : eventEnd;

        /// <summary>
        /// Plasserer en vakt "nærmest vaktlista". Vaktstart velges blant
        /// <c>eventStart.Date + d + startTime</c> for d ∈ {-1, 0, +1} dager: kandidaten med minst avstand
        /// til intervallet [eventStart, eventEnd] (0 hvis innenfor) vinner, ved likhet foretrekkes d = 0,
        /// deretter d = +1. Vaktslutt er <c>start.Date + endTime</c>, og neste døgn hvis det er før start.
        /// Vaktlistas slutt normaliseres med <see cref="NormalizeEventEnd"/> først.
        /// </summary>
        public static (DateTime Start, DateTime End) Place(DateTime eventStart, DateTime eventEnd, TimeSpan startTimeOfDay, TimeSpan endTimeOfDay)
        {
            eventEnd = NormalizeEventEnd(eventStart, eventEnd);

            DateTime start = default;
            var bestDistance = TimeSpan.MaxValue;
            foreach (var offset in DayOffsetsInPriorityOrder)
            {
                var candidate = eventStart.Date.AddDays(offset) + startTimeOfDay;
                var distance = DistanceToInterval(candidate, eventStart, eventEnd);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    start = candidate;
                }
            }

            var end = start.Date + endTimeOfDay;
            if (end < start) end = end.AddDays(1);

            return (start, end);
        }

        /// <summary>
        /// Hvor mange hele døgn vaktlista er flyttet: tidsforskyvningen fra <paramref name="oldEventStart"/> til
        /// <paramref name="newEventStart"/> rundet til nærmeste hele døgn. En endret starttid som krysser midnatt
        /// (f.eks. 1. feb 00:30 → 31. jan 23:30) gir derfor 0, ikke -1. Nøyaktig ±12 timer rundes bort fra null (±1 døgn).
        /// </summary>
        public static TimeSpan DayShift(DateTime oldEventStart, DateTime newEventStart) =>
            TimeSpan.FromDays(Math.Round((newEventStart - oldEventStart).TotalDays, MidpointRounding.AwayFromZero));

        /// <summary>
        /// Justerer tidene på en bemannet vakt når ressursen flyttes fra (<paramref name="oldStart"/>, <paramref name="oldEnd"/>)
        /// til (<paramref name="newStart"/>, <paramref name="newEnd"/>) (#173):
        /// <list type="number">
        /// <item>Kant-forankring: vaktstart lik ressursens gamle start blir ny start, vaktslutt lik gammel slutt blir ny slutt.</item>
        /// <item>Øvrige endepunkter beholder klokkeslettet, men flyttes <paramref name="dayShift"/>: like mange hele døgn som
        /// vaktlista er flyttet (se <see cref="DayShift"/>). Ressursens egen start kan bytte døgn uten at vaktlista flyttes
        /// (f.eks. 23:00 → 00:00 i en vaktliste over midnatt), så den brukes ikke.</item>
        /// <item>Klipping til ressursen: start = max(start, newStart), slutt = min(slutt, newEnd).</item>
        /// <item>Havner vakten helt utenfor (start &gt;= slutt etter klippingen), får den ressursens fulle nye tider. Unntak: en vakt
        /// som hadde null lengde fra før (start = slutt), og som fortsatt ligger innenfor ressursen, beholdes som nullengde.</item>
        /// </list>
        /// Et <c>null</c>-felt følger allerede ressursen og forblir <c>null</c>; i beregningen tolkes det som ressursens kant.
        /// Er ressursens tider uendret, returneres vaktens tider urørt.
        /// </summary>
        public static (DateTime? Start, DateTime? End) FollowResource(
            DateTime oldStart, DateTime oldEnd, DateTime newStart, DateTime newEnd, TimeSpan dayShift,
            DateTime? shiftStart, DateTime? shiftEnd)
        {
            if (oldStart == newStart && oldEnd == newEnd)
                return (shiftStart, shiftEnd);

            var oldShiftStart = shiftStart ?? oldStart;
            var oldShiftEnd = shiftEnd ?? oldEnd;
            var wasZeroLength = oldShiftStart == oldShiftEnd;

            var start = oldShiftStart == oldStart ? newStart : oldShiftStart + dayShift;
            var end = oldShiftEnd == oldEnd ? newEnd : oldShiftEnd + dayShift;

            if (start < newStart) start = newStart;
            if (end > newEnd) end = newEnd;

            // Klippet ned til null lengde (f.eks. 10–14 når ressursen blir 14–17) gir fulle tider; var vakten nullengde fra før, beholdes den.
            if (start > end || (start == end && !wasZeroLength))
                (start, end) = (newStart, newEnd);

            return (shiftStart.HasValue ? start : null, shiftEnd.HasValue ? end : null);
        }

        private static TimeSpan DistanceToInterval(DateTime value, DateTime from, DateTime to)
        {
            if (value < from) return from - value;
            if (value > to) return value - to;
            return TimeSpan.Zero;
        }
    }
}
