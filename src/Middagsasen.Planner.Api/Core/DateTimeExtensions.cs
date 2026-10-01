namespace Middagsasen.Planner.Api.Core
{
    public static class DateTimeExtensions
    {
        public const int SeasonStartMonth = 7;
        public const int FirstSeasonStartYear = 2023;
        public const string IsoDateTime = "yyyy'-'MM'-'dd'T'HH':'mm':'ssK";
        public const string IsoSimpleDateTime = "yyyy'-'MM'-'dd'T'HH':'mm";

        /// <summary>
        /// Returns datetime as ISO 8601 complete date and time string with time zone information using the <seealso cref="IsoDateTime"/> format.
        /// </summary>
        /// <param name="dateTime"></param>
        /// <returns></returns>
        public static string ToIsoString(this DateTime dateTime)
        {
            return dateTime.ToString(IsoDateTime);
        }

        /// <summary>
        /// Returns datetime as ISO 8601 date and hour+minute component of time string without time zone information using the <seealso cref="IsoSimpleDateTime"/> format.
        /// </summary>
        /// <param name="dateTime"></param>
        /// <returns></returns>
        public static string? ToSimpleIsoString(this DateTime? dateTime)
        {
            return dateTime.HasValue ? dateTime.Value.ToSimpleIsoString() : null;
        }
        public static string ToSimpleIsoString(this DateTime dateTime)
        {
            return dateTime.ToString(IsoSimpleDateTime);
        }        
        
        /// <summary>
        /// Returns the DateTime value as UTC DateTime if unspecified. If the value is Local it is converted to UTC.
        /// </summary>
        /// <param name="dateTime"></param>
        /// <returns>DateTime with <seealso cref="DateTimeKind.Utc" /></returns>
        public static DateTime AsUtc(this DateTime dateTime)
        {
            if (dateTime.Kind == DateTimeKind.Unspecified)
                return DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
            if (dateTime.Kind == DateTimeKind.Local)
                return dateTime.ToUniversalTime();
            //if (dateTime.Kind == DateTimeKind.Utc)
            return dateTime;
        }
        public static DateTime? AsUtc(this DateTime? dateTime)
        {
            if (!dateTime.HasValue) return null;
            return dateTime.Value.AsUtc();
        }

        /// <summary>
        /// Tidssonen sesonggrensene defineres i (1. juli 00:00 norsk tid).
        /// "Europe/Oslo" (IANA) støttes på både Windows og Linux i .NET 8.
        /// </summary>
        public static readonly TimeZoneInfo SeasonTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Oslo");

        /// <summary>
        /// Returnerer sesongnavn (f.eks. "2024/2025") for en <b>kalenderdato</b> (lokal/unspecified tid).
        /// Ren kalenderlogikk uten tidssonekonvertering — se <see cref="GetSeasonStartYear(DateTime)"/>.
        /// </summary>
        public static string ToSeason(this DateTime? startTime)
        {
            if (!startTime.HasValue) return "";

            return ToSeasonLabel(startTime.Value.GetSeasonStartYear());
        }

        /// <summary>
        /// Returnerer startåret for sesongen en <b>kalenderdato</b> tilhører. En sesong starter 1. <see cref="SeasonStartMonth"/>.
        /// Datoens <see cref="DateTime.Kind"/> ignoreres; ingen tidssonekonvertering gjøres.
        /// Bruk <see cref="GetSeasonStartYear(DateTimeOffset)"/> for et tidspunkt (f.eks. «nå» eller en UTC-lagret verdi).
        /// </summary>
        public static int GetSeasonStartYear(this DateTime date)
        {
            return date.Month < SeasonStartMonth ? date.Year - 1 : date.Year;
        }

        /// <summary>
        /// Returnerer startåret for sesongen et <b>tidspunkt</b> (instant) tilhører, vurdert i norsk tid
        /// (<see cref="SeasonTimeZone"/>). F.eks. 2025-06-30T22:30Z (= 1. juli 00:30 i Oslo) gir 2025.
        /// </summary>
        public static int GetSeasonStartYear(this DateTimeOffset instant)
        {
            return TimeZoneInfo.ConvertTime(instant, SeasonTimeZone).DateTime.GetSeasonStartYear();
        }

        /// <summary>
        /// Returnerer sesongen som starter i <paramref name="startYear"/> som et halvåpent intervall [From, To)
        /// av <b>UTC-tidspunkter</b> (<see cref="DateTimeKind.Utc"/>): fra 1. juli 00:00 norsk tid i
        /// <paramref name="startYear"/> til 1. juli 00:00 norsk tid året etter. Brukes mot UTC-lagrede verdier.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Hvis <paramref name="startYear"/> gir en ugyldig dato.</exception>
        public static (DateTime From, DateTime To) GetSeasonRangeUtc(int startYear)
        {
            return (SeasonStartUtc(startYear), SeasonStartUtc(startYear + 1));
        }

        private static DateTime SeasonStartUtc(int year)
        {
            var localStart = new DateTime(year, SeasonStartMonth, 1, 0, 0, 0, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(localStart, SeasonTimeZone);
        }

        /// <summary>
        /// Returnerer visningsnavn for sesongen, f.eks. "2024/2025".
        /// </summary>
        public static string ToSeasonLabel(int startYear)
        {
            return $"{startYear}/{startYear + 1}";
        }
    }
}
