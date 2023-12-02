namespace Middagsasen.Planner.Api.Data
{
    public class User
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = null!;
        public string? OneTimePassword { get; set; }
        public DateTime? OtpCreated { get; set; }
        public byte[]? EncryptedPassword { get; set; }
        public byte[]? Salt { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public bool IsHidden { get; set; }
        public bool IsAdmin { get; set; }
        public DateTime Created { get; set; }
        public bool Inactive { get; set; }

        public virtual ICollection<UserSession> Sessions { get; set; } = new HashSet<UserSession>();
        public virtual ICollection<EventResourceUser> Shifts { get; set; } = new HashSet<EventResourceUser>();
    }
}
