namespace Middagsasen.Planner.Api.Services.ResourceTypes
{
    public class FileUploadResponse
    {
        public int Id { get; set; }
        public int ResourceTypeId { get; set; }
        public string Description { get; internal set; } = null!;
        public string FileName { get; internal set; } = null!;
        public string MimeType { get; internal set; } = null!;
        public string Created { get; internal set; } = null!;
        public string CreatedBy { get; internal set; } = null!;
        public string Updated { get; internal set; } = null!;
        public string UpdatedBy { get; internal set; } = null!; 
    }
}