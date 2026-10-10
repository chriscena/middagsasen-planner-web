namespace Middagsasen.Planner.Api.Tests.Infrastructure
{
    /// <summary>
    /// Brukernavn til testbrukere. Lagrede brukernavn er normaliserte norske telefonnumre (8 sifre), og de må være
    /// unike i databasen som deles mellom testene (indeksen <c>IX_Users_UserName</c>).
    /// </summary>
    public static class TestPhoneNumbers
    {
        /// <summary>Et tilfeldig gyldig norsk telefonnummer på 8 sifre, som brukernavn.</summary>
        public static string Unique() => Random.Shared.Next(40000000, 99999999).ToString();

        /// <summary>
        /// Et unikt brukernavn som ikke er et telefonnummer (uten sifre, slik at det ikke ved et uhell normaliseres til
        /// et gyldig nummer), som eldre brukere med brukernavnet «admin». Selve «admin» brukes av andre tester.
        /// </summary>
        public static string UniqueInvalid()
            => "admin_" + string.Concat(Guid.NewGuid().ToString("N").Select(c => char.IsAsciiDigit(c) ? (char)('g' + (c - '0')) : c));
    }
}
