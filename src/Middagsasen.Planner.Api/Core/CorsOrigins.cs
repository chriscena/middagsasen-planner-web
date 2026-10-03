namespace Middagsasen.Planner.Api.Core
{
    /// <summary>Leser opphavene som skal tillates av CORS fra <c>Cors:AllowedOrigins</c>.</summary>
    public static class CorsOrigins
    {
        public const string SectionName = "Cors:AllowedOrigins";

        /// <summary>
        /// Godtar både en liste (JSON-array, eller miljøvariabler <c>Cors__AllowedOrigins__0</c>, <c>__1</c>, ...) og én
        /// kommaseparert streng (f.eks. miljøvariabelen <c>Cors__AllowedOrigins=https://a,https://b</c>).
        /// Tomme verdier og mellomrom fjernes, og duplikater slås sammen. Tom liste betyr at CORS er av (kun samme opphav).
        /// </summary>
        public static string[] Read(IConfiguration configuration)
        {
            var section = configuration.GetSection(SectionName);
            var values = section.GetChildren().Select(child => child.Value).Append(section.Value);
            return values
                .SelectMany(value => (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }
}
