using Middagsasen.Planner.Api.Services.Users;

namespace Middagsasen.Planner.Api.Data
{
    public class UserSession
    {
        public Guid UserSessionId { get; set; } = Guid.NewGuid();
        public int UserId { get; set; }
        public DateTime Created { get; set; } = DateTime.UtcNow;
        public AuthType AuthType { get; set; }

        public User? User { get; set; }
    }
}
