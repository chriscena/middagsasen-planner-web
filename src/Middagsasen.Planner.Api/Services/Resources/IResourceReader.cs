using Middagsasen.Planner.Api.Services.Events;
using Middagsasen.Planner.Api.Services.ResourceTypes;

namespace Middagsasen.Planner.Api.Services.Resources
{
    /// <summary>
    /// Lesemodulen for arrangementer, oppgaver, vakttyper, opplæring, meldinger og filer. Eier hvilke tabeller som
    /// lastes og all mapping til DTO-ene, inkludert fulle navn, flaggene fra <see cref="Shifts.ShiftRules"/> og
    /// kompetansevarslene. Kallerne trenger derfor ikke kjenne til includes eller mappe disse DTO-ene selv.
    /// <para>
    /// Samme <see cref="ResourceTypeResponse"/> gis uansett om vakttypen leses via arrangement, mal eller vakttype.
    /// Modulen eier mappingen av oppgaver og vakttyper, ikke av maler: skallet til malene
    /// (<see cref="EventTemplateResponse"/>/<see cref="ResourceTemplateResponse"/>) mappes i <see cref="EventTemplatesService"/>,
    /// som henter vakttypene via <see cref="GetResourceTypes(IEnumerable{int})"/>.
    /// </para>
    /// <para>
    /// Implementasjonen er et bevisst unntak fra repository-mønsteret: en ren lesemodell (spørringer og mapping, uten
    /// forretningsregler for skriving) som går rett mot <c>PlannerDbContext</c>. Skrivesiden bruker fortsatt repository.
    /// </para>
    /// <para>
    /// Datoformat: lokal norsk tid uten sone (arrangement, oppgave, vakt) som <c>yyyy-MM-ddTHH:mm</c>;
    /// UTC-tidspunkter (filer, meldinger, opplæringens bekreftelse) som <c>yyyy-MM-ddTHH:mm:ssZ</c>.
    /// </para>
    /// </summary>
    public interface IResourceReader
    {
        /// <summary>
        /// Arrangementene med oppgaver og flagg for <paramref name="actor"/>. <paramref name="start"/> og
        /// <paramref name="end"/> avgrenser på arrangementets start (<c>start &lt;= StartTime &lt; end</c>), og hver av dem
        /// kan utelates.
        /// </summary>
        Task<IReadOnlyList<EventResponse>> GetEvents(Actor actor, DateTime? start = null, DateTime? end = null);

        /// <summary>Arrangementet med oppgaver og flagg for <paramref name="actor"/>, eller <c>null</c>.</summary>
        Task<EventResponse?> GetEvent(Actor actor, int eventId);

        /// <summary>Oppgaven med vakter, meldinger og flagg for <paramref name="actor"/>, eller <c>null</c>.</summary>
        Task<ResourceResponse?> GetResource(Actor actor, int resourceId);

        /// <summary>Alle aktive vakttyper.</summary>
        Task<IReadOnlyList<ResourceTypeResponse>> GetResourceTypes();

        /// <summary>
        /// Vakttypene med de gitte id-ene, også inaktive (maler og arrangementer kan peke på dem). Id-er som ikke finnes,
        /// er ikke med i svaret.
        /// </summary>
        Task<IReadOnlyDictionary<int, ResourceTypeResponse>> GetResourceTypes(IEnumerable<int> ids);

        /// <summary>Vakttypen (også inaktiv), eller <c>null</c>.</summary>
        Task<ResourceTypeResponse?> GetResourceType(int id);

        /// <summary>Opplæringen med vakttype og hvem som bekreftet den, eller <c>null</c>.</summary>
        Task<TrainingResponse?> GetTraining(int trainingId);

        /// <summary>Meldingene på oppgaven, eldste først.</summary>
        Task<IReadOnlyList<MessageResponse>> GetMessages(int resourceId);

        /// <summary>Meldingen, eller <c>null</c>.</summary>
        Task<MessageResponse?> GetMessage(int messageId);

        /// <summary>Filinformasjonen (uten innhold), eller <c>null</c>.</summary>
        Task<FileInfoResponse?> GetFile(int fileId);
    }
}
