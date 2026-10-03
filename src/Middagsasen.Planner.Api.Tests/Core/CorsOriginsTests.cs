using Microsoft.Extensions.Configuration;
using Middagsasen.Planner.Api.Core;

namespace Middagsasen.Planner.Api.Tests.Core
{
    public class CorsOriginsTests
    {
        private static string[] Read(params (string Key, string? Value)[] values)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(values.ToDictionary(v => v.Key, v => v.Value))
                .Build();
            return CorsOrigins.Read(configuration);
        }

        [Fact]
        public void ReadsArray()
        {
            var origins = Read(("Cors:AllowedOrigins:0", "https://a.no"), ("Cors:AllowedOrigins:1", "https://b.no"));

            Assert.Equal(new[] { "https://a.no", "https://b.no" }, origins);
        }

        [Fact]
        public void ReadsCommaSeparatedString()
        {
            // Som miljøvariabelen Cors__AllowedOrigins=https://a.no, https://b.no
            var origins = Read(("Cors:AllowedOrigins", "https://a.no, https://b.no,"));

            Assert.Equal(new[] { "https://a.no", "https://b.no" }, origins);
        }

        [Fact]
        public void ReadsEnvironmentVariable()
        {
            // Eget prefiks, så testen ikke påvirkes av eller påvirker andre miljøvariabler.
            const string prefix = "CorsOriginsTests_";
            Environment.SetEnvironmentVariable($"{prefix}Cors__AllowedOrigins", "https://a.no,https://b.no");
            try
            {
                var configuration = new ConfigurationBuilder().AddEnvironmentVariables(prefix).Build();
                Assert.Equal(new[] { "https://a.no", "https://b.no" }, CorsOrigins.Read(configuration));
            }
            finally
            {
                Environment.SetEnvironmentVariable($"{prefix}Cors__AllowedOrigins", null);
            }
        }

        [Fact]
        public void IgnoresEmptyValues_AndRemovesDuplicates()
        {
            var origins = Read(("Cors:AllowedOrigins:0", "https://a.no"), ("Cors:AllowedOrigins:1", " "), ("Cors:AllowedOrigins:2", "https://a.no"));

            Assert.Equal(new[] { "https://a.no" }, origins);
        }

        [Fact]
        public void ReturnsEmpty_WhenNotConfigured()
        {
            Assert.Empty(Read());
            Assert.Empty(Read(("Cors:AllowedOrigins", "")));
        }
    }
}
