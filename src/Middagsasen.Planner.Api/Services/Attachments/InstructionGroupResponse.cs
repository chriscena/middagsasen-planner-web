namespace Middagsasen.Planner.Api.Services.Attachments
{
    /// <summary>
    /// Instruksene på én vakttype for innlogget bruker (<c>GET api/me/instructions</c>). Bare aktive vedlegg med
    /// <c>RequiresAcknowledgement</c>; vakttyper uten instrukser utelates.
    /// </summary>
    public class InstructionGroupResponse
    {
        public int ResourceTypeId { get; set; }
        public string ResourceTypeName { get; set; } = null!;

        /// <summary>Vakttypen gjelder innlogget bruker: brukeren har vakt på den fra og med inneværende sesongstart.</summary>
        public bool AppliesToMe { get; set; }

        /// <summary>Instruksene, med <see cref="FileInfoResponse.AcknowledgedAt"/> for innlogget bruker.</summary>
        public IEnumerable<FileInfoResponse> Files { get; set; } = new List<FileInfoResponse>();
    }
}
