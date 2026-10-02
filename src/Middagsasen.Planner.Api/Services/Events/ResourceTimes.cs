namespace Middagsasen.Planner.Api.Services.Events
{
    /// <summary>
    /// Bestemmer hvilket døgn en vakt (ressurs) i en vaktliste havner på, gitt bare klokkeslett.
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

        private static TimeSpan DistanceToInterval(DateTime value, DateTime from, DateTime to)
        {
            if (value < from) return from - value;
            if (value > to) return value - to;
            return TimeSpan.Zero;
        }
    }
}
