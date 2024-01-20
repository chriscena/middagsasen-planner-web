namespace Middagsasen.Planner.Api.Services.ResourceTypes
{
    public class FileUploadRequest
    {
        public IFormFile FileInfo { get; set; } = null!;
        public string Description { get; set; } = null!;
        public int UserId { get; set; }
    }
}