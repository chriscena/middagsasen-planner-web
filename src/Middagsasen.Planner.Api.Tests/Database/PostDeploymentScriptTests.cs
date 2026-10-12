using Microsoft.Data.SqlClient;
using Middagsasen.Planner.Api.Tests.Infrastructure;

namespace Middagsasen.Planner.Api.Tests.Database
{
    /// <summary>
    /// Tester Scripts/Script.PostDeployment.sql i databaseprosjektet, som fyller ut WorkHours.CreatedBy og
    /// WorkHours.CreatedTime der de mangler (CreatedBy = UserId, CreatedTime = StartTime). Hver test bruker en egen
    /// database i containeren, så delte testdata ikke berøres. Databasene slettes etter hver test.
    /// </summary>
    [Collection("Database")]
    public class PostDeploymentScriptTests : IAsyncLifetime
    {
        private readonly DatabaseFixture _fixture;
        private readonly List<string> _databases = [];

        public PostDeploymentScriptTests(DatabaseFixture fixture)
        {
            _fixture = fixture;
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public async Task DisposeAsync()
        {
            // Tøm connection pool først, ellers holder poolede tilkoblinger databasene i bruk.
            SqlConnection.ClearAllPools();
            await using var master = new SqlConnection(_fixture.ConnectionString);
            await master.OpenAsync();
            foreach (var name in _databases)
            {
                await new SqlCommand(
                    $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}];",
                    master).ExecuteNonQueryAsync();
            }
        }

        private static readonly Lazy<string> Script = new(() => ReadScript("Script.PostDeployment.sql"));

        private static string ReadScript(string fileName)
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

        private async Task<string> CreateDatabase()
        {
            var name = $"PostDeploy_{Guid.NewGuid():N}";
            await using (var master = new SqlConnection(_fixture.ConnectionString))
            {
                await master.OpenAsync();
                await new SqlCommand($"CREATE DATABASE [{name}]", master).ExecuteNonQueryAsync();
            }
            _databases.Add(name);
            return new SqlConnectionStringBuilder(_fixture.ConnectionString) { InitialCatalog = name }.ConnectionString;
        }

        /// <summary>
        /// Users og WorkHours. Med <paramref name="withCreatedColumns"/> slik tabellen ser ut etter skjemaendringen
        /// (som i Tables/WorkHours.sql), ellers slik den så ut før.
        /// </summary>
        private static async Task CreateTables(string connectionString, bool withCreatedColumns = true)
        {
            await Execute(connectionString, """
                create table Users (
                    UserId int not null IDENTITY,
                    CONSTRAINT PK_Users PRIMARY key (UserId),
                    UserName NVARCHAR(100) not null,
                );
                create table WorkHours (
                    WorkHourId int not null identity,
                    constraint PK_WorkHours PRIMARY key (WorkHourId),
                    UserId int not null,
                    constraint FK_WorkHours_Users_UserId foreign key (UserId) references Users(UserId),
                    StartTime DATETIME not null,
                    EndTime DATETIME null,
                );
                insert into Users (UserName) values ('eier1'), ('eier2'), ('admin');
                """);
            if (withCreatedColumns)
            {
                await Execute(connectionString, """
                    alter table WorkHours add
                        CreatedBy INT null,
                        constraint FK_WorkHours_Users_CreatedBy foreign key (CreatedBy) references Users(UserId),
                        CreatedTime DATETIME null;
                    """);
            }
        }

        /// <summary>Kjører SQL og returnerer meldingene fra <c>PRINT</c>.</summary>
        private static async Task<List<string>> Execute(string connectionString, string sql)
        {
            var messages = new List<string>();
            await using var connection = new SqlConnection(connectionString);
            connection.InfoMessage += (_, e) => messages.Add(e.Message);
            await connection.OpenAsync();
            await new SqlCommand(sql, connection).ExecuteNonQueryAsync();
            return messages;
        }

        private static async Task<List<(int UserId, DateTime StartTime, int? CreatedBy, DateTime? CreatedTime)>> ReadWorkHours(string connectionString)
        {
            var result = new List<(int, DateTime, int?, DateTime?)>();
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var reader = await new SqlCommand(
                "select UserId, StartTime, CreatedBy, CreatedTime from WorkHours order by WorkHourId", connection).ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add((
                    reader.GetInt32(0),
                    reader.GetDateTime(1),
                    reader.IsDBNull(2) ? null : reader.GetInt32(2),
                    reader.IsDBNull(3) ? null : reader.GetDateTime(3)));
            }
            return result;
        }

        private static readonly DateTime Start1 = new(2025, 1, 10, 9, 0, 0);
        private static readonly DateTime Start2 = new(2025, 2, 20, 10, 30, 0);
        private static readonly DateTime Created = new(2026, 10, 1, 12, 0, 0);

        [Fact]
        public async Task FillsMissingValues_FromUserIdAndStartTime_AndKeepsExisting()
        {
            var connectionString = await CreateDatabase();
            await CreateTables(connectionString);
            await Execute(connectionString, """
                -- Gamle rader (eller rader fra gammelt API): mangler begge
                insert into WorkHours (UserId, StartTime) values (1, '2025-01-10T09:00:00'), (2, '2025-02-20T10:30:00');
                -- Ført av admin (3) for eier 1 med nytt API: urørt
                insert into WorkHours (UserId, StartTime, CreatedBy, CreatedTime) values (1, '2025-01-10T09:00:00', 3, '2026-10-01T12:00:00');
                """);

            var messages = await Execute(connectionString, Script.Value);

            Assert.Equal(
                [(1, Start1, 1, Start1), (2, Start2, 2, Start2), (1, Start1, 3, Created)],
                await ReadWorkHours(connectionString));
            Assert.Contains(messages, m => m.Contains("CreatedBy på 2 rader") && m.Contains("CreatedTime på 2 rader"));
        }

        [Fact]
        public async Task IsIdempotent_AndSilentWhenNothingToFill()
        {
            var connectionString = await CreateDatabase();
            await CreateTables(connectionString);
            await Execute(connectionString, "insert into WorkHours (UserId, StartTime) values (1, '2025-01-10T09:00:00')");

            await Execute(connectionString, Script.Value);
            var first = await ReadWorkHours(connectionString);
            var messages = await Execute(connectionString, Script.Value);

            Assert.Equal(first, await ReadWorkHours(connectionString));
            Assert.Equal([(1, Start1, 1, Start1)], first);
            Assert.DoesNotContain(messages, m => m.Contains("Fylte ut"));
        }

        /// <summary>
        /// Databasen deployes før API-et. Rader som det gamle API-et lager i mellomtiden (uten de nye kolonnene)
        /// fylles ut ved neste deploy, siden skriptet kjøres ved hver deploy.
        /// </summary>
        [Fact]
        public async Task FillsRowsCreatedByOldApiAfterPreviousDeploy()
        {
            var connectionString = await CreateDatabase();
            await CreateTables(connectionString);
            await Execute(connectionString, "insert into WorkHours (UserId, StartTime) values (1, '2025-01-10T09:00:00')");
            await Execute(connectionString, Script.Value);

            // Gammelt API: insert uten CreatedBy/CreatedTime.
            await Execute(connectionString, "insert into WorkHours (UserId, StartTime, EndTime) values (2, '2025-02-20T10:30:00', '2025-02-20T12:00:00')");
            var messages = await Execute(connectionString, Script.Value);

            Assert.Equal([(1, Start1, 1, Start1), (2, Start2, 2, Start2)], await ReadWorkHours(connectionString));
            Assert.Contains(messages, m => m.Contains("CreatedBy på 1 rader"));
        }

        [Fact]
        public async Task DoesNothing_OnEmptyTable()
        {
            var connectionString = await CreateDatabase();
            await CreateTables(connectionString);

            var messages = await Execute(connectionString, Script.Value);

            Assert.Empty(await ReadWorkHours(connectionString));
            Assert.DoesNotContain(messages, m => m.Contains("Fylte ut"));
        }

        [Fact]
        public async Task DoesNothing_WhenWorkHoursTableDoesNotExist()
        {
            var connectionString = await CreateDatabase();

            await Execute(connectionString, Script.Value);
        }

        [Fact]
        public async Task DoesNothing_WhenColumnsDoNotExist()
        {
            var connectionString = await CreateDatabase();
            await CreateTables(connectionString, withCreatedColumns: false);
            await Execute(connectionString, "insert into WorkHours (UserId, StartTime) values (1, '2025-01-10T09:00:00')");

            var messages = await Execute(connectionString, Script.Value);

            Assert.DoesNotContain(messages, m => m.Contains("Fylte ut"));
        }
    }
}
