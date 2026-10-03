using Middagsasen.Planner.Api.Services;
using Middagsasen.Planner.Api.Services.Users;

namespace Middagsasen.Planner.Api.Authentication
{
    public interface ICurrentUserService
    {
        UserResponse? User { get; }
        int UserId { get; }
        bool IsAdmin { get; }
    }

    public static class CurrentUserServiceExtensions
    {
        /// <summary>Innlogget bruker som <see cref="Actor"/>, til bruk i tilgangspolicyene.</summary>
        /// <exception cref="NotAuthenticatedException">Ingen innlogget bruker.</exception>
        public static Actor ToActor(this ICurrentUserService currentUser) => new(currentUser.UserId, currentUser.IsAdmin);
    }
}
