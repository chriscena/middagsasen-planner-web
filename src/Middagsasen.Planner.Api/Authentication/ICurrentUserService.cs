using Middagsasen.Planner.Api.Services;

namespace Middagsasen.Planner.Api.Authentication
{
    public interface ICurrentUserService
    {
        /// <summary>Innlogget bruker, eller <c>null</c> hvis forespørselen ikke er autentisert.</summary>
        Actor? Actor { get; }

        /// <exception cref="NotAuthenticatedException">Ingen innlogget bruker.</exception>
        int UserId { get; }

        /// <summary>Om innlogget bruker er administrator. <c>false</c> hvis ingen er innlogget.</summary>
        bool IsAdmin { get; }
    }

    public static class CurrentUserServiceExtensions
    {
        /// <summary>Innlogget bruker som <see cref="Actor"/>, til bruk i tilgangspolicyene.</summary>
        /// <exception cref="NotAuthenticatedException">Ingen innlogget bruker.</exception>
        public static Actor ToActor(this ICurrentUserService currentUser) => new(currentUser.UserId, currentUser.IsAdmin);
    }
}
