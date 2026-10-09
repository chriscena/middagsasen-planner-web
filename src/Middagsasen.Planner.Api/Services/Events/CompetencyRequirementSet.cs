using Microsoft.EntityFrameworkCore;
using Middagsasen.Planner.Api.Data;

namespace Middagsasen.Planner.Api.Services.Events
{
    /// <summary>
    /// Validering, lagring, kopiering og mapping av anleggskrav, felles for vaktlister (<see cref="EventCompetencyRequirement"/>)
    /// og maler (<see cref="EventTemplateCompetencyRequirement"/>).
    /// </summary>
    /// <remarks>
    /// Krav til inaktive (slettede) kompetanser blir stående i databasen, men vises ikke (<see cref="Active{T}"/>), kopieres
    /// ikke mellom mal og vaktliste, og kan ikke settes. Siden klienten sender hele listen den fikk, fjernes de dermed ved neste
    /// lagring av vaktlisten eller malen (<see cref="Apply{T}"/> fjerner krav som mangler); <c>null</c> lar dem stå urørt.
    /// </remarks>
    internal static class CompetencyRequirementSet
    {
        internal const string MissingRequirementMessage = "Anleggskravet mangler.";
        internal const string MinimumRequiredTooLowMessage = "Antall i et anleggskrav må være minst 1.";
        internal const string DuplicateCompetencyMessage = "Samme kompetanse kan bare ha ett anleggskrav.";
        internal const string UnknownCompetencyMessage = "Fant ikke kompetansen i anleggskravet.";
        internal const string InactiveCompetencyMessage = "Kompetansen i anleggskravet er slettet.";

        /// <summary>
        /// Validerer kravene: ingen tomme elementer, antall minst 1, ingen kompetanse to ganger, og kompetansene må finnes og
        /// være aktive. <c>null</c> er gyldig (ingen endring).
        /// </summary>
        /// <exception cref="DomainValidationException">Kravene er ugyldige.</exception>
        public static async Task Validate(PlannerDbContext dbContext, IEnumerable<CompetencyRequirementRequest?>? requests)
        {
            if (requests is null)
                return;

            var list = requests.ToList();
            if (list.Any(r => r is null))
                throw new DomainValidationException(MissingRequirementMessage);

            if (list.Any(r => r!.MinimumRequired < 1))
                throw new DomainValidationException(MinimumRequiredTooLowMessage);

            var competencyIds = list.Select(r => r!.CompetencyId).ToList();
            if (competencyIds.Distinct().Count() != competencyIds.Count)
                throw new DomainValidationException(DuplicateCompetencyMessage);

            if (competencyIds.Count == 0)
                return;

            var found = await dbContext.Competencies
                .Where(c => competencyIds.Contains(c.CompetencyId))
                .Select(c => c.Inactive)
                .ToListAsync();
            if (found.Count != competencyIds.Count)
                throw new DomainValidationException(UnknownCompetencyMessage);
            if (found.Any(inactive => inactive))
                throw new DomainValidationException(InactiveCompetencyMessage);
        }

        /// <summary>
        /// Erstatter settet med <paramref name="requests"/> (diff på kompetanse): endret antall oppdateres, nye legges til og
        /// manglende fjernes (også krav til inaktive kompetanser, som klienten aldri får se). <c>null</c> lar settet være
        /// uendret. Må valideres med <see cref="Validate"/> først.
        /// </summary>
        public static void Apply<T>(
            ICollection<T> existing,
            IEnumerable<CompetencyRequirementRequest>? requests,
            Func<T, int> competencyId,
            Action<T, int> setMinimumRequired,
            Func<int, int, T> create)
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
                existing.Add(create(request.CompetencyId, request.MinimumRequired));
        }

        public static void Apply(ICollection<EventCompetencyRequirement> existing, IEnumerable<CompetencyRequirementRequest>? requests)
            => Apply(existing, requests, e => e.CompetencyId, (e, min) => e.MinimumRequired = min, NewEventRequirement);

        public static void Apply(ICollection<EventTemplateCompetencyRequirement> existing, IEnumerable<CompetencyRequirementRequest>? requests)
            => Apply(existing, requests, e => e.CompetencyId, (e, min) => e.MinimumRequired = min, NewTemplateRequirement);

        /// <summary>Kopierer malens krav til en ny vaktliste, uten krav til inaktive kompetanser. Krever <c>Competency</c>.</summary>
        public static List<EventCompetencyRequirement> CopyToEvent(IEnumerable<EventTemplateCompetencyRequirement> source)
            => Active(source, r => r.Competency)
                .Select(r => NewEventRequirement(r.CompetencyId, r.MinimumRequired))
                .ToList();

        /// <summary>Kopierer vaktlistens krav til en ny mal, uten krav til inaktive kompetanser. Krever <c>Competency</c>.</summary>
        public static List<EventTemplateCompetencyRequirement> CopyToTemplate(IEnumerable<EventCompetencyRequirement> source)
            => Active(source, r => r.Competency)
                .Select(r => NewTemplateRequirement(r.CompetencyId, r.MinimumRequired))
                .ToList();

        /// <summary>Kravene til aktive kompetanser. Krever <c>Competency</c>.</summary>
        public static IEnumerable<EventCompetencyRequirement> Active(IEnumerable<EventCompetencyRequirement> requirements)
            => Active(requirements, r => r.Competency);

        /// <summary>Kravene til aktive kompetanser. Krever <c>Competency</c>.</summary>
        public static IEnumerable<EventTemplateCompetencyRequirement> Active(IEnumerable<EventTemplateCompetencyRequirement> requirements)
            => Active(requirements, r => r.Competency);

        private static IEnumerable<T> Active<T>(IEnumerable<T> requirements, Func<T, Competency?> competency)
            => requirements.Where(r => competency(r) is { Inactive: false });

        private static EventCompetencyRequirement NewEventRequirement(int competencyId, int minimumRequired) => new()
        {
            CompetencyId = competencyId,
            MinimumRequired = minimumRequired,
        };

        private static EventTemplateCompetencyRequirement NewTemplateRequirement(int competencyId, int minimumRequired) => new()
        {
            CompetencyId = competencyId,
            MinimumRequired = minimumRequired,
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

        /// <summary>Kravene til aktive kompetanser, sortert på kompetansenavn (og id ved likt navn). Krever <c>Competency</c>.</summary>
        public static List<CompetencyRequirementResponse> MapActive(IEnumerable<EventCompetencyRequirement> requirements)
            => Sorted(Active(requirements).Select(Map));

        /// <summary>Kravene til aktive kompetanser, sortert på kompetansenavn (og id ved likt navn). Krever <c>Competency</c>.</summary>
        public static List<CompetencyRequirementResponse> MapActive(IEnumerable<EventTemplateCompetencyRequirement> requirements)
            => Sorted(Active(requirements).Select(Map));

        private static List<CompetencyRequirementResponse> Sorted(IEnumerable<CompetencyRequirementResponse> responses)
            => responses.OrderBy(r => r.CompetencyName).ThenBy(r => r.CompetencyId).ToList();
    }
}
