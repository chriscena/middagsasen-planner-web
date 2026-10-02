using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Middagsasen.Planner.Api.Data;
using Testcontainers.MsSql;

namespace Middagsasen.Planner.Api.Tests.Infrastructure
{
    /// <summary>
    /// Test-specific DbContext that resolves SQL Server multiple cascade path issues.
    /// Changes secondary FK cascade deletes to NoAction where multiple paths from Users
    /// would otherwise cause "may cause cycles or multiple cascade paths" errors.
    /// </summary>
    internal class TestPlannerDbContext : PlannerDbContext
    {
        public TestPlannerDbContext(DbContextOptions<PlannerDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ResourceTypeFiles has two FK paths to Users (CreatedBy + UpdatedBy)
            modelBuilder.Entity<ResourceTypeFile>(entity =>
            {
                entity.HasOne(e => e.CreatedByUser)
                    .WithMany(c => c.CreatedResourceTypeFiles)
                    .HasForeignKey(d => d.CreatedBy)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(e => e.UpdatedByUser)
                    .WithMany(c => c.UpdatedResourceTypeFiles)
                    .HasForeignKey(d => d.UpdatedBy)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // EventResourceMessages has cascade path via EventResources and via Users
            modelBuilder.Entity<EventResourceMessage>(entity =>
            {
                entity.HasOne(e => e.CreatedByUser)
                    .WithMany(c => c.CreatedMessages)
                    .HasForeignKey(d => d.CreatedBy)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // EventResourceUsers has cascade path via EventResources and via Users
            modelBuilder.Entity<EventResourceUser>(entity =>
            {
                entity.HasOne(e => e.User)
                    .WithMany(c => c.Shifts)
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // ResourceTypeTrainings has two FK paths to Users (UserId + ConfirmedBy)
            modelBuilder.Entity<ResourceTypeTraining>(entity =>
            {
                entity.HasOne(e => e.ConfirmedByUser)
                    .WithMany(c => c.ConfirmedTrainings)
                    .HasForeignKey(d => d.ConfirmedBy)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // UserCompetencies has two FK paths to Users (UserId + ApprovedBy)
            modelBuilder.Entity<UserCompetency>(entity =>
            {
                entity.HasOne(e => e.ApprovedByUser)
                    .WithMany(c => c.ApprovedUserCompetencies)
                    .HasForeignKey(d => d.ApprovedBy)
                    .OnDelete(DeleteBehavior.NoAction);
            });
        }
    }

    public class DatabaseFixture : IAsyncLifetime
    {
        private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
            .Build();

        public string ConnectionString => _container.GetConnectionString();

        public async Task InitializeAsync()
        {
            await _container.StartAsync();

            // Create schema using test context that resolves cascade path issues
            var options = new DbContextOptionsBuilder<PlannerDbContext>()
                .UseSqlServer(ConnectionString)
                .Options;
            using var context = new TestPlannerDbContext(options);
            await context.Database.EnsureCreatedAsync();

            // EnsureCreated hopper over entiteter mappet med ToView, så viewene må opprettes manuelt.
            // Definisjonen er hentet fra Middagsasen.Planner.Database/Views/HallOfFame.sql.
            await context.Database.ExecuteSqlRawAsync(HallOfFameViewSql);
        }

        private const string HallOfFameViewSql = """
            create view HallOfFame as
              select u.UserId, u.FirstName, u.LastName, count(distinct er.EventId) Shifts  from Users u
              join EventResourceUsers eru on eru.UserId = u.UserId
              join EventResources er on er.EventResourceId = eru.EventResourceId
              Where eru.EndTime < cast(cast(GETDATE() as date) as datetime)
              GROUP by u.UserId, u.FirstName, u.LastName
            """;

        public async Task DisposeAsync()
        {
            await _container.DisposeAsync();
        }

        public PlannerDbContext CreateContext(params IInterceptor[] interceptors)
        {
            var options = new DbContextOptionsBuilder<PlannerDbContext>()
                .UseSqlServer(ConnectionString)
                .AddInterceptors(interceptors)
                .Options;
            return new PlannerDbContext(options);
        }
    }
}
