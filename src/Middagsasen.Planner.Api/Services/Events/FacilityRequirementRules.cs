namespace Middagsasen.Planner.Api.Services.Events
{
    /// <summary>
    /// Regler for anleggskrav: minst et gitt antall av de som er på vakt i anlegget skal ha en bestemt kompetanse på hvert
    /// tidspunkt i åpningstiden, uansett vakttype. Ren og uten avhengigheter. Alle tidsrom er halvåpne, [start, slutt).
    /// </summary>
    public static class FacilityRequirementRules
    {
        /// <summary>En bemannet vakt for en bruker med (gyldig) kompetanse.</summary>
        public readonly record struct StaffedPeriod(int UserId, DateTime Start, DateTime End);

        /// <summary>Et sammenhengende tidsrom der bare <see cref="Count"/> ulike brukere med kompetansen er på vakt.</summary>
        public readonly record struct Breach(DateTime Start, DateTime End, int Count);

        /// <summary>
        /// Vaktens faktiske tidsrom: vaktens egne tider, og oppgavens tider der vakten ikke har egne
        /// (samme tolkning som ved påmelding i <c>ShiftService</c>).
        /// </summary>
        public static (DateTime Start, DateTime End) EffectivePeriod(
            DateTime? shiftStart, DateTime? shiftEnd, DateTime resourceStart, DateTime resourceEnd)
            => (shiftStart ?? resourceStart, shiftEnd ?? resourceEnd);

        /// <summary>
        /// Tidsrommene i åpningstiden [<paramref name="openStart"/>, <paramref name="openEnd"/>) der færre enn
        /// <paramref name="minimumRequired"/> ulike brukere fra <paramref name="periods"/> er på vakt. Vaktene klippes til
        /// åpningstiden, og samme bruker teller én gang selv med overlappende vakter. Tilstøtende tidsrom med samme antall slås
        /// sammen. Tom liste når kravet er oppfylt hele tiden (eller åpningstiden er tom).
        /// </summary>
        public static IReadOnlyList<Breach> FindBreaches(
            DateTime openStart, DateTime openEnd, int minimumRequired, IEnumerable<StaffedPeriod> periods)
        {
            var breaches = new List<Breach>();
            if (openEnd <= openStart)
                return breaches;

            var clipped = periods
                .Select(p => p with
                {
                    Start = p.Start < openStart ? openStart : p.Start,
                    End = p.End > openEnd ? openEnd : p.End,
                })
                .Where(p => p.Start < p.End)
                .ToList();

            var breakpoints = clipped
                .SelectMany(p => new[] { p.Start, p.End })
                .Append(openStart)
                .Append(openEnd)
                .Distinct()
                .Order()
                .ToList();

            for (var i = 0; i < breakpoints.Count - 1; i++)
            {
                var start = breakpoints[i];
                var end = breakpoints[i + 1];

                // Ingen vakt starter eller slutter inne i segmentet, så den som er på vakt ved start, er det hele segmentet.
                var count = clipped
                    .Where(p => p.Start <= start && p.End > start)
                    .Select(p => p.UserId)
                    .Distinct()
                    .Count();

                if (count >= minimumRequired)
                    continue;

                if (breaches.Count > 0 && breaches[^1] is { } last && last.End == start && last.Count == count)
                    breaches[^1] = last with { End = end };
                else
                    breaches.Add(new Breach(start, end, count));
            }

            return breaches;
        }
    }
}
