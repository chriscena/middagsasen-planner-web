using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using System.Data;
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
        }
    }
}
