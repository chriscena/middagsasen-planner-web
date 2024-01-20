namespace Middagsasen.Planner.Api.Data
{
    public class ResourceTypeFile
    {
        public int ResourceTypeFileId { get; set; }
        public string StorageName { get; set; } = null!;
        public string FileName { get; set; } = null!;
        public string Description { get; set; } = null!;
        public string MimeType { get; set; } = null!;
        public int ResourceTypeId { get; set; }
        public DateTime Created { get; set; }
        public int CreatedBy { get; set; }
        public DateTime Updated { get; set; }
        public int UpdatedBy { get; set; }

        public ResourceType ResourceType { get; set; } = null!;
        public User CreatedByUser { get; set; } = null!;
        public User UpdatedByUser { get; set; } = null!;
    }
}