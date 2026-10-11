namespace Middagsasen.Planner.Api.Services.Attachments
{
    /// <summary>Innholdet i et vedlegg, til nedlasting.</summary>
    public class FileResponse
    {
        public byte[] Data { get; set; } = null!;
        public string MimeType { get; set; } = null!;
        public string FileName { get; set; } = null!;
    }
}
