using Middagsasen.Planner.Api.Data;

namespace Middagsasen.Planner.Api.Services.Attachments
{
    /// <summary>Dataadgang for vedlegg, instrukser og kvitteringer.</summary>
    public interface IAttachmentRepository
    {
        /// <summary>Vedlegget (aktivt eller arkivert) med tracking, med navnene mappingen trenger, eller <c>null</c>.</summary>
        Task<ResourceTypeFile?> Get(int fileId, int resourceTypeId);

        /// <summary>Arkiverte vedlegg på vakttypen, nyeste arkivering først.</summary>
        Task<IReadOnlyList<ResourceTypeFile>> GetArchived(int resourceTypeId);

        /// <summary>Alle aktive instrukser (vedlegg med <c>RequiresAcknowledgement</c>) på aktive vakttyper, med vakttype og navn.</summary>
        Task<IReadOnlyList<ResourceTypeFile>> GetActiveInstructions();

        /// <summary>Lagrer et nytt vedlegg.</summary>
        Task<ResourceTypeFile> Add(ResourceTypeFile file);

        /// <summary>Lagrer endringer på hentede entiteter.</summary>
        Task SaveChangesAsync();

        /// <summary>Brukerens kvittering på vedlegget, eller <c>null</c>.</summary>
        Task<ResourceTypeFileAcknowledgement?> GetAcknowledgement(int fileId, int userId);

        /// <summary>
        /// Lagrer en kvittering. Finnes den fra før (samtidig kvittering, unik indeks), returneres den eksisterende i stedet.
        /// </summary>
        Task<ResourceTypeFileAcknowledgement> Acknowledge(ResourceTypeFileAcknowledgement acknowledgement);

        /// <summary>Alle kvitteringer på vedlegget, med bruker.</summary>
        Task<IReadOnlyList<ResourceTypeFileAcknowledgement>> GetAcknowledgements(int fileId);

        /// <summary>Når brukeren kvitterte, per vedlegg brukeren har kvittert for.</summary>
        Task<IReadOnlyDictionary<int, DateTime>> GetAcknowledgedFileIds(int userId);

        /// <summary>
        /// Instruksene som sperrer brukeren fra egen påmelding på vakttypen: aktive, påkrevde instrukser brukeren ikke har
        /// kvittert for, sortert på beskrivelse. SQL-utgaven av <see cref="AttachmentRules.BlockingInstructions"/>.
        /// </summary>
        Task<IReadOnlyList<ResourceTypeFile>> GetUnacknowledgedMandatory(int userId, int resourceTypeId);

        /// <summary>
        /// Vakttypene brukeren har vakt på med oppgavestart fra og med <paramref name="fromUtc"/> (regelen «gjelder deg»).
        /// Oppgavenes tider er norsk lokal tid, så grensen konverteres før sammenligningen.
        /// </summary>
        Task<IReadOnlySet<int>> GetResourceTypesWithShiftsSince(int userId, DateTime fromUtc);

        /// <summary>Brukerne (aktive) som har vakt på vakttypen med oppgavestart fra og med <paramref name="fromUtc"/>.</summary>
        Task<IReadOnlySet<int>> GetUsersWithShiftsSince(int resourceTypeId, DateTime fromUtc);

        /// <summary>Aktive brukere (ikke <c>Inactive</c>), til kvitteringsrapporten.</summary>
        Task<IReadOnlyList<User>> GetActiveUsers();
    }
}
