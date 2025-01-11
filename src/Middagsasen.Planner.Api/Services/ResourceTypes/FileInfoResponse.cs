namespace Middagsasen.Planner.Api.Services.ResourceTypes
{
    public class FileInfoResponse
    {
        public int Id { get; set; }
        public int ResourceTypeId { get; set; }
        public string FileName { get; set; } = null!;
        public string Description { get; set; } = null!;
        public string Created { get; set; } = null!;
        public string CreatedBy { get; set; } = null!;
        public string Updated { get; set; } = null!;
        public string UpdatedBy { get; set; } = null!;
        public string MimeType { get; set; } = null!;
    }
}