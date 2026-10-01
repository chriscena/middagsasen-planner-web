using System.Runtime.CompilerServices;

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

        public static string ToSeason(this DateTime? startTime)
        {
            if (!startTime.HasValue) return "";

            return ToSeasonLabel(startTime.Value.GetSeasonStartYear());
        }

        /// <summary>
        /// Returnerer startåret for sesongen datoen tilhører. En sesong starter 1. <see cref="SeasonStartMonth"/>.
        /// </summary>
        public static int GetSeasonStartYear(this DateTime date)
        {
            return date.Month < SeasonStartMonth ? date.Year - 1 : date.Year;
        }

        /// <summary>
        /// Returnerer datointervallet for sesongen som starter i <paramref name="startYear"/>,
        /// som et halvåpent intervall [From, To).
        /// </summary>
        public static (DateTime From, DateTime To) GetSeasonRange(int startYear)
        {
            return (new DateTime(startYear, SeasonStartMonth, 1), new DateTime(startYear + 1, SeasonStartMonth, 1));
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
