using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Data;
using static Microsoft.ApplicationInsights.MetricDimensionNames.TelemetryContext;
using System.Security.Principal;

namespace Middagsasen.Planner.Api.Data
{
    public class PlannerDbContext : DbContext
    {

        public PlannerDbContext()
        {
        }

        public PlannerDbContext(DbContextOptions<PlannerDbContext> options)
            : base(options)
        {
        }

        public virtual DbSet<User> Users { get; set; } = null!;
        public virtual DbSet<UserSession> UserSessions { get; set; } = null!;
        public virtual DbSet<ResourceType> ResourceTypes { get; set; } = null!;
        public virtual DbSet<Event> Events { get; set; } = null!;
        public virtual DbSet<EventResourceUser> Shifts { get; set; } = null!;
        public virtual DbSet<EventResourceMessage> Messages { get; set; } = null!;
        public virtual DbSet<EventTemplate> EventTemplates { get; set; } = null!;
        public virtual DbSet<HallOfFamer> HallOfFamers { get; set; } = null!;
        public virtual DbSet<EventStatus> EventStatuses { get; set; } = null!;
        public virtual DbSet<ResourceTypeTrainer> ResourceTypeTrainers { get;set; } = null!;
        public virtual DbSet<ResourceTypeTraining> ResourceTypeTrainings { get; set; } = null!;
        public virtual DbSet<ResourceTypeFile> ResourceTypeFiles { get; set; } = null!;
        public virtual DbSet<WeatherLocation> WeatherLocations { get; set; } = null!;
        public virtual DbSet<WeatherMeasurement> WeatherMeasurements { get; set; } = null!;
        public virtual DbSet<WeatherMeasurementValue> WeatherMeasurementValues { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.UserId);
                entity.Property(e => e.UserName).HasMaxLength(100);
                entity.Property(e => e.OneTimePassword).HasMaxLength(400);
                entity.Property(e => e.OtpCreated).HasColumnType("datetime");
                entity.Property(e => e.OneTimePassword).HasMaxLength(400);
                entity.Property(e => e.EncryptedPassword).HasMaxLength(400);
                entity.Property(e => e.Salt).HasMaxLength(400);
                entity.Property(e => e.FirstName).HasMaxLength(400);
                entity.Property(e => e.LastName).HasMaxLength(400);
                entity.Property(e => e.Created).HasColumnType("datetime");
            });

            modelBuilder.Entity<UserSession>(entity =>
            {
                entity.HasKey(e => e.UserSessionId);
                entity.Property(e => e.Created).HasColumnType("datetime");

                entity.HasOne(e => e.User)
                    .WithMany(c => c.Sessions)
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("FK_UserSessions_Users");
            });

            modelBuilder.Entity<ResourceType>(entity =>
            {
                entity.HasKey(e => e.ResourceTypeId);
                entity.Property(e => e.Name).HasMaxLength(400);
            });

            modelBuilder.Entity<Event>(entity =>
            {
                entity.HasKey(e => e.EventId);
                entity.Property(e => e.Name).HasMaxLength(400);
                entity.Property(e => e.StartTime).HasColumnType("datetime");
                entity.Property(e => e.EndTime).HasColumnType("datetime");
            });

            modelBuilder.Entity<EventResource>(entity =>
            {
                entity.ToTable("EventResources");
                entity.HasKey(e => e.EventResourceId);
                entity.Property(e => e.StartTime).HasColumnType("datetime");
                entity.Property(e => e.EndTime).HasColumnType("datetime");

                entity.HasOne(e => e.Event)
                    .WithMany(c => c.Resources)
                    .HasForeignKey(d => d.EventId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("FK_EventResources_Events");

                entity.HasOne(e => e.ResourceType)
                    .WithMany(c => c.Resources)
                    .HasForeignKey(d => d.ResourceTypeId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("FK_EventResources_ResourceTypes");
            });

            modelBuilder.Entity<EventResourceUser>(entity =>
            {
                entity.ToTable("EventResourceUsers");
                entity.HasKey(e => e.EventResourceUserId);
                entity.Property(e => e.StartTime).HasColumnType("datetime");
                entity.Property(e => e.EndTime).HasColumnType("datetime");

                entity.HasOne(e => e.Resource)
                    .WithMany(c => c.Shifts)
                    .HasForeignKey(d => d.EventResourceId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("FK_EventResourceUsers_EventResources");

                entity.HasOne(e => e.User)
                    .WithMany(c => c.Shifts)
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("FK_EventResourceUsers_Users");
            });

            modelBuilder.Entity<EventResourceMessage>(entity =>
            {
                entity.ToTable("EventResourceMessages");
                entity.HasKey(e => e.EventResourceMessageId);
                entity.Property(e => e.Created).HasColumnType("datetime");

                entity.HasOne(e => e.Resource)
                    .WithMany(c => c.Messages)
                    .HasForeignKey(d => d.EventResourceId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("FK_EventResourceMessages_EventResources");

                entity.HasOne(e => e.CreatedByUser)
                    .WithMany(c => c.CreatedMessages)
                    .HasForeignKey(d => d.CreatedBy)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("FK_EventResourceMessages_Users_Created");
            });

            modelBuilder.Entity<EventTemplate>(entity =>
            {
                entity.HasKey(e => e.EventTemplateId);
                entity.Property(e => e.Name).HasMaxLength(400);
                entity.Property(e => e.EventName).HasMaxLength(400);
                entity.Property(e => e.StartTime).HasColumnType("datetime");
                entity.Property(e => e.EndTime).HasColumnType("datetime");
            });

            modelBuilder.Entity<ResourceTemplate>(entity =>
            {
                entity.ToTable("ResourceTemplates");
                entity.HasKey(e => e.ResourceTemplateId);
                entity.Property(e => e.StartTime).HasColumnType("datetime");
                entity.Property(e => e.EndTime).HasColumnType("datetime");

                entity.HasOne(e => e.EventTemplate)
                    .WithMany(c => c.ResourceTemplates)
                    .HasForeignKey(d => d.EventTemplateId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("FK_ResourceTemplates_EventTemplates");

                entity.HasOne(e => e.ResourceType)
                    .WithMany(c => c.ResourceTemplates)
                    .HasForeignKey(d => d.ResourceTypeId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("FK_ResourceTemplates_ResourceTypes");
            });

            modelBuilder.Entity<HallOfFamer>(entity =>
            {
                entity.ToView("HallOfFame");
                entity.HasKey(e => e.UserId);
            });

            modelBuilder.Entity<EventStatus>(entity =>
            {
                entity.ToView("EventStatuses");
                entity.HasNoKey();
                entity.Property(e => e.StartTime).HasColumnType("datetime");
            });

            modelBuilder.Entity<ResourceTypeTrainer>(entity =>
            {
                entity.ToTable("ResourceTypeTrainers");
                entity.HasKey(e => e.ResourceTypeTrainerId);

                entity.HasOne(e => e.ResourceType)
                    .WithMany(c => c.Trainers)
                    .HasForeignKey(d => d.ResourceTypeId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("FK_ResourceTypeTrainers_ResourceTypes");

                entity.HasOne(e => e.User)
                    .WithMany(c => c.ResourceTypeTrainers)
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("FK_ResourceTypeTrainers_Users");

                entity.HasIndex(e => new { e.ResourceTypeId, e.UserId })
                    .IsUnique()
                    .HasDatabaseName("UQ_ResourceTypeTrainers_ResourceTypeId_UserId");
            });

            modelBuilder.Entity<ResourceTypeTraining>(entity =>
            {
                entity.ToTable("ResourceTypeTrainings");
                entity.HasKey(e => e.ResourceTypeTrainingId);
                entity.Property(e => e.Confirmed).HasColumnType("datetime");

                entity.HasOne(e => e.User)
                    .WithMany(c => c.Trainings)
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("FK_ResourceTypeTrainings_Users");

                entity.HasOne(e => e.ResourceType)
                    .WithMany(c => c.Trainings)
                    .HasForeignKey(d => d.ResourceTypeId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("FK_ResourceTypeTrainings_ResourceTypes");

                entity.HasIndex(e => new { e.UserId, e.ResourceTypeId })
                    .IsUnique()
                    .HasDatabaseName("UQ_ResourceTypeTrainings_UserId_ResourceTypeId");


                entity.HasOne(e => e.ConfirmedByUser)
                    .WithMany(c => c.ConfirmedTrainings)
                    .HasForeignKey(d => d.ConfirmedBy)
                    .OnDelete(DeleteBehavior.SetNull)
                    .HasConstraintName("FK_ResourceTypeTrainings_Users_ConfirmedBy");
            });

            modelBuilder.Entity<ResourceTypeFile>(entity =>
            {
                entity.ToTable("ResourceTypeFiles");
                entity.HasKey(e => e.ResourceTypeFileId);

                entity.HasOne(e => e.ResourceType)
                    .WithMany(c => c.Files)
                    .HasForeignKey(d => d.ResourceTypeId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("FK_ResourceTypeFiles_ResourceTypes");

                entity.HasOne(e => e.CreatedByUser)
                    .WithMany(c => c.CreatedResourceTypeFiles)
                    .HasForeignKey(d => d.CreatedBy)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("FK_ResourceTypeFiles_Users_CreatedBy");

                entity.HasOne(e => e.UpdatedByUser)
                    .WithMany(c => c.UpdatedResourceTypeFiles)
                    .HasForeignKey(d => d.UpdatedBy)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("FK_ResourceTypeFiles_Users_UpdatedBy");

            });

            modelBuilder.Entity<WeatherLocation>(entity =>
            {
                entity.ToTable("WeatherLocations");
                entity.HasKey(e => e.WeatherLocationId);

                entity.Property(e => e.LocationName).HasMaxLength(400);
            });

            modelBuilder.Entity<WeatherMeasurement>(entity =>
            {
                entity.ToTable("WeatherMeasurements");
                entity.HasKey(e => e.WeatherMeasurementId);

                entity.Property(e => e.MeasurementLabel).HasMaxLength(400);
            });

            modelBuilder.Entity<WeatherMeasurementValue>(entity =>
            {
                entity.ToTable("WeatherMeasurementValues");
                entity.HasKey(e => e.WeatherMeasurementValueId);
                entity.Property(e => e.MeasuredValue).HasColumnType("decimal(15, 5)");
                entity.Property(e => e.MeasuredTime).HasColumnType("datetime");

                entity.HasOne(e => e.WeatherMeasurement)
                    .WithMany(c => c.Values)
                    .HasForeignKey(d => d.WeatherMeasurementId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("FK_WeatherMeasurementValues_WeatherMeasurements");

                entity.HasOne(e => e.WeatherLocation)
                    .WithMany(c => c.Values)
                    .HasForeignKey(d => d.WeatherLocationId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("FK_WeatherMeasurementValues_WeatherLocations");
            });
        }
    }
}