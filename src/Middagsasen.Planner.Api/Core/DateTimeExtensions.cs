namespace Middagsasen.Planner.Api.Core
{
    public static class DateTimeExtensions
    {
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
    }
}
