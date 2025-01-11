namespace Middagsasen.Planner.Api.Services.ResourceTypes
{
    public class FileResponse
    {
        public byte[] Data { get; set; } = null!;
        public string MimeType { get; set; } = null!;
        public string FileName { get; set; } = null!;
    }
}