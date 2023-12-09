namespace Middagsasen.Planner.Api.Core
{
    public static class StringExtensions
    {

        public static string ToNumericString(this string input)
        {
            return string.Concat(input.Where(char.IsDigit));
        }

        public static long ToNumericPhoneNo(this string input)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(input)) return 0;
                var phoneNo = long.Parse(input.ToNumericString());
                if (phoneNo < 10000000) return 0;
                if (phoneNo <= 99999999) phoneNo += 4700000000;
                return phoneNo;
            }
            catch (Exception)
            {
                return 0;
            }
        }
    }
}
