using Microsoft.Data.SqlClient;
using Middagsasen.Planner.Api.Tests.Infrastructure;

namespace Middagsasen.Planner.Api.Tests.Database
{
    /// <summary>
    /// Felles oppsett for tester av skriptene i Middagsasen.Planner.Database/Scripts. Hver test lager egne databaser i
    /// containeren (<see cref="CreateEmptyDatabase"/>), så delte testdata ikke berøres. Databasene slettes etter hver test.
    /// </summary>
    public abstract class DeploymentScriptTestBase : IAsyncLifetime
    {
        private readonly List<string> _databases = [];

        protected DeploymentScriptTestBase(DatabaseFixture fixture)
        {
            Fixture = fixture;
        }

        protected DatabaseFixture Fixture { get; }

        public Task InitializeAsync() => Task.CompletedTask;

        public async Task DisposeAsync()
        {
            // Tøm connection pool først, ellers holder poolede tilkoblinger databasene i bruk.
            SqlConnection.ClearAllPools();
            await using var master = new SqlConnection(Fixture.ConnectionString);
            await master.OpenAsync();
            foreach (var name in _databases)
            {
                await new SqlCommand(
                    $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}];",
                    master).ExecuteNonQueryAsync();
            }
        }

        /// <summary>Leser et skript fra src/Middagsasen.Planner.Database/Scripts, funnet ved å gå oppover fra testkatalogen.</summary>
        protected static string ReadScript(string fileName)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var path = Path.Combine(dir.FullName, "src", "Middagsasen.Planner.Database", "Scripts", fileName);
                if (File.Exists(path)) return File.ReadAllText(path);
                dir = dir.Parent;
            }
            throw new FileNotFoundException($"Fant ikke {fileName}");
        }

        /// <summary>Lager en tom database som slettes etter testen, og returnerer connection string til den.</summary>
        protected async Task<string> CreateEmptyDatabase(string prefix)
        {
            var name = $"{prefix}_{Guid.NewGuid():N}";
            await using (var master = new SqlConnection(Fixture.ConnectionString))
            {
                await master.OpenAsync();
                await new SqlCommand($"CREATE DATABASE [{name}]", master).ExecuteNonQueryAsync();
            }
            _databases.Add(name);
            return new SqlConnectionStringBuilder(Fixture.ConnectionString) { InitialCatalog = name }.ConnectionString;
        }

        /// <summary>Kjører SQL og returnerer meldingene fra <c>PRINT</c>.</summary>
        protected static async Task<List<string>> Execute(string connectionString, string sql)
        {
            var messages = new List<string>();
            await using var connection = new SqlConnection(connectionString);
            connection.InfoMessage += (_, e) => messages.Add(e.Message);
            await connection.OpenAsync();
            await new SqlCommand(sql, connection).ExecuteNonQueryAsync();
            return messages;
        }
    }
}
