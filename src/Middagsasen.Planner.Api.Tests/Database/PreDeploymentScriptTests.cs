using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services.Users;
using Middagsasen.Planner.Api.Tests.Infrastructure;

namespace Middagsasen.Planner.Api.Tests.Database
{
    /// <summary>
    /// Tester Scripts/Script.PreDeployment.sql i databaseprosjektet, som normaliserer Users.UserName før den unike
    /// indeksen opprettes. Hver test bruker en egen database i containeren, så delte testdata ikke berøres.
    /// </summary>
    [Collection("Database")]
    public class PreDeploymentScriptTests
    {
        private readonly DatabaseFixture _fixture;

        public PreDeploymentScriptTests(DatabaseFixture fixture)
        {
            _fixture = fixture;
        }

        private static readonly Lazy<string> Script = new(() => ReadScript("Script.PreDeployment.sql"));

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

        private async Task<string> CreateDatabase(bool withUsersTable = true)
        {
            var name = $"PreDeploy_{Guid.NewGuid():N}";
            await using (var master = new SqlConnection(_fixture.ConnectionString))
            {
                await master.OpenAsync();
                await new SqlCommand($"CREATE DATABASE [{name}]", master).ExecuteNonQueryAsync();
            }

            var connectionString = new SqlConnectionStringBuilder(_fixture.ConnectionString) { InitialCatalog = name }.ConnectionString;
            if (withUsersTable)
            {
                // Uten den unike indeksen, slik tabellen ser ut før deploy.
                await Execute(connectionString, """
                    create table Users (
                        UserId int not null IDENTITY,
                        CONSTRAINT PK_Users PRIMARY key (UserId),
                        UserName NVARCHAR(100) not null,
                        Inactive bit not null CONSTRAINT DF_Users_Inactive DEFAULT 0,
                    )
                    """);
            }
            return connectionString;
        }

        private static async Task Execute(string connectionString, string sql)
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await new SqlCommand(sql, connection).ExecuteNonQueryAsync();
        }

        private static async Task<Dictionary<int, string>> InsertUsers(string connectionString, params (string UserName, bool Inactive)[] users)
        {
            var ids = new Dictionary<int, string>();
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            foreach (var (userName, inactive) in users)
            {
                var command = new SqlCommand("insert into Users (UserName, Inactive) output inserted.UserId values (@userName, @inactive)", connection);
                command.Parameters.AddWithValue("@userName", userName);
                command.Parameters.AddWithValue("@inactive", inactive);
                ids.Add((int)(await command.ExecuteScalarAsync())!, userName);
            }
            return ids;
        }

        private static async Task<Dictionary<int, string>> ReadUserNames(string connectionString)
        {
            var result = new Dictionary<int, string>();
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var reader = await new SqlCommand("select UserId, UserName from Users", connection).ExecuteReaderAsync();
            while (await reader.ReadAsync())
                result.Add(reader.GetInt32(0), reader.GetString(1));
            return result;
        }

        [Fact]
        public async Task NormalizesUserNames_ExactlyLikeToNormalizedUserName()
        {
            var connectionString = await CreateDatabase();
            string[] userNames =
            [
                "12345678",
                "+47 22345678",
                "4732345678",
                "004742345678",
                "523 45 678",
                "+47 623 45 678",
                "72345678 ",
                "0000000000000000000000082345678",
                "4692345678",
                "123456789",
                "9223372036854775807",
                "9223372036854775808",
                "12345678901234567890123",
                "admin",
                "Ola Nordmann",
                "1234",
                "1234567",
                "",
            ];
            var ids = await InsertUsers(connectionString, userNames.Select(u => (u, false)).ToArray());

            await Execute(connectionString, Script.Value);

            var stored = await ReadUserNames(connectionString);
            foreach (var (id, original) in ids)
            {
                var expected = original.ToNormalizedUserName() ?? original;
                Assert.True(string.Equals(expected, stored[id], StringComparison.Ordinal),
                    $"«{original}»: forventet «{expected}», fikk «{stored[id]}»");
            }

            // Stikkprøver med eksplisitte verdier, så testen ikke bare sammenligner med C#-implementasjonen.
            string StoredFor(string original) => stored[ids.Single(i => i.Value == original).Key];
            Assert.Equal("12345678", StoredFor("12345678"));
            Assert.Equal("22345678", StoredFor("+47 22345678"));
            Assert.Equal("32345678", StoredFor("4732345678"));
            Assert.Equal("42345678", StoredFor("004742345678"));
            Assert.Equal("52345678", StoredFor("523 45 678"));
            Assert.Equal("72345678", StoredFor("72345678 "));
            Assert.Equal("82345678", StoredFor("0000000000000000000000082345678"));
            Assert.Equal("92345678", StoredFor("4692345678"));
            Assert.Equal("3456789", StoredFor("123456789"));
            Assert.Equal("23372036854775807", StoredFor("9223372036854775807"));
            Assert.Equal("9223372036854775808", StoredFor("9223372036854775808"));
            Assert.Equal("admin", StoredFor("admin"));
            Assert.Equal("1234", StoredFor("1234"));
        }

        [Fact]
        public async Task IsIdempotent()
        {
            var connectionString = await CreateDatabase();
            await InsertUsers(connectionString, ("+47 12345678", false), ("admin", false), ("22345678", true));

            await Execute(connectionString, Script.Value);
            var first = await ReadUserNames(connectionString);
            await Execute(connectionString, Script.Value);
            var second = await ReadUserNames(connectionString);

            Assert.Equal(first, second);
            Assert.Equal(["12345678", "admin", "22345678"], second.OrderBy(u => u.Key).Select(u => u.Value));
        }

        [Fact]
        public async Task DoesNothing_WhenUsersTableDoesNotExist()
        {
            var connectionString = await CreateDatabase(withUsersTable: false);

            await Execute(connectionString, Script.Value);
        }

        [Fact]
        public async Task StopsWithoutChanges_WhenNormalizationGivesCollisions()
        {
            var connectionString = await CreateDatabase();
            var ids = await InsertUsers(connectionString,
                ("12345678", false),
                ("+47 12345678", true),
                ("+47 22345678", false),
                ("0047 22345678", false),
                ("+47 32345678", false),
                ("admin", false));
            var before = await ReadUserNames(connectionString);

            var ex = await Assert.ThrowsAsync<SqlException>(() => Execute(connectionString, Script.Value));

            Assert.Equal(50000, ex.Number);
            Assert.Contains("slå sammen", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("«12345678»", ex.Message);
            Assert.Contains("«22345678»", ex.Message);
            Assert.DoesNotContain("«32345678»", ex.Message);
            foreach (var (id, userName) in ids.Where(i => i.Value.Contains("12345678") || i.Value.Contains("22345678")))
                Assert.Contains($"{id} («{userName}»", ex.Message);
            Assert.Equal(before, await ReadUserNames(connectionString));
        }
    
        [Fact]
        public async Task FinnBrukernavnKollisjoner_ListsCollidingUsers()
        {
            // Hele skjemaet, siden spørringen teller vakter, timeføringer osv. Uten den unike indeksen, slik det ser ut før deploy.
            var connectionString = await CreateDatabase(withUsersTable: false);
            var options = new DbContextOptionsBuilder<PlannerDbContext>().UseSqlServer(connectionString).Options;
            await using (var context = new TestPlannerDbContext(options))
                await context.Database.EnsureCreatedAsync();
            await Execute(connectionString, """
                drop index IX_Users_UserName on Users;
                -- Gir resten av kolonnene standardverdier, så InsertUsers kan brukes som mot den minimale tabellen.
                alter table Users add default 0 for IsHidden, default 0 for IsAdmin, default getutcdate() for Created;
                """);

            var ids = await InsertUsers(connectionString,
                ("12345678", false),
                ("+47 12345678", true),
                ("+47 22345678", false),
                ("admin", false));

            var rows = new List<(string NyttBrukernavn, int UserId)>();
            await using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                await using var reader = await new SqlCommand(ReadScript("FinnBrukernavnKollisjoner.sql"), connection).ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    rows.Add((reader.GetString(reader.GetOrdinal("NyttBrukernavn")), reader.GetInt32(reader.GetOrdinal("UserId"))));
            }

            Assert.Equal(
                ids.Where(i => i.Value.EndsWith("12345678")).Select(i => ("12345678", i.Key)).OrderBy(r => r.Key),
                rows.OrderBy(r => r.UserId));
        }
    }
}
