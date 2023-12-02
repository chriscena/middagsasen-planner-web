namespace Middagsasen.Planner.Api.Services.Events
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
        public static string ToSimpleIsoString(this DateTime dateTime)
        {
            return dateTime.ToString(IsoSimpleDateTime);
        }
    }
}
