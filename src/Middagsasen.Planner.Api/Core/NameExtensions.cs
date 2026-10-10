using Middagsasen.Planner.Api.Data;

namespace Middagsasen.Planner.Api.Core
{
    /// <summary>
    /// Navnet til en bruker, lest med en lett projeksjon når bare navnet trengs (f.eks. i en SMS), så passordhash og
    /// resten av <see cref="User"/> ikke hentes. Formateres med <see cref="NameExtensions.FullName(PersonName)"/>.
    /// </summary>
    public sealed record PersonName(string? FirstName, string? LastName);

    /// <summary>Felles regel for hvordan en brukers fulle navn vises i DTO-er og meldinger.</summary>
    public static class NameExtensions
    {
        /// <summary>Fullt navn: fornavn og etternavn med mellomrom, uten mellomrom i endene når et av dem mangler.</summary>
        public static string FullName(string? firstName, string? lastName)
            => $"{firstName ?? ""} {lastName ?? ""}".Trim();

        /// <summary>Fullt navn til brukeren (se <see cref="FullName(string?, string?)"/>).</summary>
        public static string FullName(this User user) => FullName(user.FirstName, user.LastName);

        /// <summary>Fullt navn fra et projisert navn (se <see cref="FullName(string?, string?)"/>).</summary>
        public static string FullName(this PersonName name) => FullName(name.FirstName, name.LastName);
    }
}
