using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Data;

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
        public virtual DbSet<EventTemplate> EventTemplates { get; set; } = null!;
        public virtual DbSet<HallOfFamer> HallOfFamers { get; set; } = null!;

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
        }
    }
}