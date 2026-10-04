using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Middagsasen.Planner.Api.Core
{
    /// <summary>
    /// Årene som godtas for lokale datoer og tider i requester. SQL <c>datetime</c> kan lagre 1753–9999, og
    /// vaktlistetider kan forskyves én dag (neste døgn over midnatt, eller døgnet før/etter for vakter), så
    /// grensene har god margin og gir 400 i stedet for en databasefeil (500).
    /// </summary>
    internal static class LocalDateRange
    {
        internal const int MinYear = 1900;
        internal const int MaxYear = 9998;

        internal static bool Contains(int year) => year is >= MinYear and <= MaxYear;
    }

    /// <summary>
    /// Lokal tid uten sone (<c>"yyyy-MM-ddTHH:mm"</c>, sekunder valgfrie). Avviser tider med <c>Z</c> eller offset og
    /// år utenfor <see cref="LocalDateRange"/>. Feilen er en <see cref="JsonException"/>, som modellbindingen gjør om til
    /// «Ugyldig verdi.» på feltet (se <see cref="ModelValidation"/>).
    /// </summary>
    /// <remarks>
    /// Brukes per egenskap med <c>[JsonConverter]</c>, ikke globalt: timeføring og vaktpåmelding sender UTC-tidspunkt
    /// med <c>Z</c> i <see cref="DateTime"/>-felt.
    /// </remarks>
    public sealed class LocalDateTimeConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String
                || !reader.TryGetDateTime(out var value)
                || value.Kind != DateTimeKind.Unspecified
                || !LocalDateRange.Contains(value.Year))
                throw new JsonException();
            return value;
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Dato <c>"yyyy-MM-dd"</c>. Avviser år utenfor <see cref="LocalDateRange"/> med <see cref="JsonException"/>
    /// («Ugyldig verdi.» på feltet, se <see cref="ModelValidation"/>).
    /// </summary>
    public sealed class LocalDateConverter : JsonConverter<DateOnly>
    {
        public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String
                || !DateOnly.TryParseExact(reader.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)
                || !LocalDateRange.Contains(value.Year))
                throw new JsonException();
            return value;
        }

        public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }
}
