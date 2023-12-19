namespace Middagsasen.Planner.Api.Data
{
    public class ResourceType
    {
        public int ResourceTypeId { get; set; }
        public string Name { get; set; } = null!;
        public int DefaultStaff { get; set; } = 1;
        public bool Inactive { get; set; }

        public virtual ICollection<EventResource> Resources { get; set; } = new HashSet<EventResource>();
        public virtual ICollection<ResourceTemplate> ResourceTemplates { get; set; } = new HashSet<ResourceTemplate>();
        public virtual ICollection<ResourceTypeTrainer> Trainers { get; set; } = new HashSet<ResourceTypeTrainer>();
        public virtual ICollection<ResourceTypeTraining> Trainings { get; set; } = new HashSet<ResourceTypeTraining>();
    }
}