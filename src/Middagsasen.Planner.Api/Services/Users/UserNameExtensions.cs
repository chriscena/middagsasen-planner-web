using Microsoft.EntityFrameworkCore;
using Middagsasen.Planner.Api.Data;

namespace Middagsasen.Planner.Api.Services.Users
{
    public static class UserNameExtensions
    {
        /// <summary>
        /// Normaliserer et telefonnummer/brukernavn til formatet som lagres i <c>Users.UserName</c> og slås opp
        /// ved innlogging og OTP: et norsk mobil-/fasttelefonnummer på 8 sifre, uten landskode.
        /// </summary>
        /// <remarks>
        /// Alle tegn som ikke er sifre (0-9) fjernes, og ledende nuller ignoreres (så «0047 …» fungerer). Gyldig er da bare
        /// <list type="bullet">
        /// <item>nøyaktig 8 sifre (10000000–99999999), eller</item>
        /// <item>47 fulgt av nøyaktig 8 sifre (4710000000–4799999999).</item>
        /// </list>
        /// Alt annet gir <c>null</c> (ugyldig), f.eks. utenlandske numre («+46 92345678»), for mange eller for få
        /// sifre, og brukernavnet «admin». Utenlandske numre må avvises og ikke kuttes til 8 sifre, ellers kunne de
        /// treffe en annen persons norske bruker. Samme regel brukes i Script.PreDeployment.sql i databaseprosjektet.
        /// </remarks>
        public static string? ToNormalizedUserName(this string? phoneNo)
        {
            var digits = string.Concat((phoneNo ?? "").Where(char.IsAsciiDigit)).TrimStart('0');
            if (digits.Length == 10 && digits.StartsWith("47"))
                digits = digits[2..];
            return digits.Length == 8 && digits[0] != '0' ? digits : null;
        }

        /// <summary>
        /// Telefonnummeret med landskode (47) for SMS, ut fra et brukernavn normalisert med <see cref="ToNormalizedUserName"/>.
        /// </summary>
        public static long ToSmsPhoneNo(this string normalizedUserName) => long.Parse($"47{normalizedUserName}");

        /// <summary>
        /// Felles oppslag på brukernavn (også inaktive brukere). <paramref name="normalizedUserName"/> må være normalisert
        /// med <see cref="ToNormalizedUserName"/>. Lagrede brukernavn er normalisert og unike (indeksen
        /// <c>IX_Users_UserName</c>), så eksakt sammenligning holder og gir høyst én bruker.
        /// </summary>
        public static IQueryable<User> WhereUserName(this IQueryable<User> users, string normalizedUserName)
            => users.Where(u => u.UserName == normalizedUserName);

        /// <summary>
        /// Lagrer endringer som setter brukernavnet <paramref name="normalizedUserName"/> på brukeren
        /// <paramref name="userId"/> (0 for en ny bruker). Returnerer <c>false</c> hvis lagringen ble avvist fordi en
        /// annen bruker (også inaktiv) nå har brukernavnet, det vil si at en parallell forespørsel tok det mellom
        /// sjekken og lagringen, og den unike indeksen avviste lagringen. Andre feil kastes videre.
        /// </summary>
        /// <remarks>
        /// Vi sjekker på nytt etter <see cref="DbUpdateException"/> i stedet for å tolke leverandørspesifikke feilkoder.
        /// Endringene som feilet ligger fortsatt i konteksten; kalleren må rydde dem før en ny lagring.
        /// </remarks>
        public static async Task<bool> TrySaveWithUniqueUserName(this PlannerDbContext dbContext, string normalizedUserName, int userId)
        {
            try
            {
                await dbContext.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateException)
            {
                if (await dbContext.Users.WhereUserName(normalizedUserName).AnyAsync(u => u.UserId != userId))
                    return false;
                throw;
            }
        }
    }
}
