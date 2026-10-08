using Microsoft.EntityFrameworkCore;
using Middagsasen.Planner.Api.Data;

namespace Middagsasen.Planner.Api.Services.Events
{
    /// <summary>
    /// Validering, lagring og mapping av anleggskrav, felles for vaktlister (<see cref="EventCompetencyRequirement"/>) og maler
    /// (<see cref="EventTemplateCompetencyRequirement"/>).
    /// </summary>
    internal static class CompetencyRequirementSet
    {
        internal const string MinimumRequiredTooLowMessage = "Antall i et anleggskrav må være minst 1.";
        internal const string DuplicateCompetencyMessage = "Samme kompetanse kan bare ha ett anleggskrav.";
        internal const string UnknownCompetencyMessage = "Fant ikke kompetansen i anleggskravet.";

        /// <summary>
        /// Validerer kravene: antall minst 1, ingen kompetanse to ganger, og kompetansene må finnes.
        /// <c>null</c> er gyldig (ingen endring).
        /// </summary>
        /// <exception cref="DomainValidationException">Kravene er ugyldige.</exception>
        public static async Task Validate(PlannerDbContext dbContext, IEnumerable<CompetencyRequirementRequest>? requests)
        {
            if (requests is null)
                return;

            var list = requests.ToList();
            if (list.Any(r => r.MinimumRequired < 1))
                throw new DomainValidationException(MinimumRequiredTooLowMessage);

            var competencyIds = list.Select(r => r.CompetencyId).ToList();
            if (competencyIds.Distinct().Count() != competencyIds.Count)
                throw new DomainValidationException(DuplicateCompetencyMessage);

            if (competencyIds.Count == 0)
                return;

            var found = await dbContext.Competencies
                .Where(c => competencyIds.Contains(c.CompetencyId))
                .CountAsync();
            if (found != competencyIds.Count)
                throw new DomainValidationException(UnknownCompetencyMessage);
        }

        /// <summary>
        /// Erstatter settet med <paramref name="requests"/> (diff på kompetanse): endret antall oppdateres, nye legges til og
        /// manglende fjernes. <c>null</c> lar settet være uendret. Må valideres med <see cref="Validate"/> først.
        /// </summary>
        public static void Apply<T>(
            ICollection<T> existing,
            IEnumerable<CompetencyRequirementRequest>? requests,
            Func<T, int> competencyId,
            Action<T, int> setMinimumRequired,
            Func<CompetencyRequirementRequest, T> create)
        {
            if (requests is null)
                return;

            var byCompetency = requests.ToDictionary(r => r.CompetencyId);

            foreach (var stored in existing.Where(e => !byCompetency.ContainsKey(competencyId(e))).ToList())
                existing.Remove(stored);

            foreach (var stored in existing)
                setMinimumRequired(stored, byCompetency[competencyId(stored)].MinimumRequired);

            var storedIds = existing.Select(competencyId).ToHashSet();
            foreach (var request in byCompetency.Values.Where(r => !storedIds.Contains(r.CompetencyId)))
                existing.Add(create(request));
        }

        public static void Apply(ICollection<EventCompetencyRequirement> existing, IEnumerable<CompetencyRequirementRequest>? requests)
            => Apply(existing, requests, e => e.CompetencyId, (e, min) => e.MinimumRequired = min, ToEventRequirement);

        public static void Apply(ICollection<EventTemplateCompetencyRequirement> existing, IEnumerable<CompetencyRequirementRequest>? requests)
            => Apply(existing, requests, e => e.CompetencyId, (e, min) => e.MinimumRequired = min, ToTemplateRequirement);

        private static EventCompetencyRequirement ToEventRequirement(CompetencyRequirementRequest request) => new()
        {
            CompetencyId = request.CompetencyId,
            MinimumRequired = request.MinimumRequired,
        };

        private static EventTemplateCompetencyRequirement ToTemplateRequirement(CompetencyRequirementRequest request) => new()
        {
            CompetencyId = request.CompetencyId,
            MinimumRequired = request.MinimumRequired,
        };

        /// <summary>Krever <c>Competency</c>.</summary>
        public static CompetencyRequirementResponse Map(EventCompetencyRequirement requirement)
            => Map(requirement.CompetencyId, requirement.Competency, requirement.MinimumRequired);

        /// <summary>Krever <c>Competency</c>.</summary>
        public static CompetencyRequirementResponse Map(EventTemplateCompetencyRequirement requirement)
            => Map(requirement.CompetencyId, requirement.Competency, requirement.MinimumRequired);

        private static CompetencyRequirementResponse Map(int competencyId, Competency competency, int minimumRequired) => new()
        {
            CompetencyId = competencyId,
            CompetencyName = competency.Name,
            MinimumRequired = minimumRequired,
        };

        /// <summary>Sorterer på kompetansenavn (og id ved likt navn).</summary>
        public static List<CompetencyRequirementResponse> Sorted(IEnumerable<CompetencyRequirementResponse> responses)
            => responses.OrderBy(r => r.CompetencyName).ThenBy(r => r.CompetencyId).ToList();
    }
}
