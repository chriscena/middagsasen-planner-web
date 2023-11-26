namespace Middagsasen.Planner.Api.Services.Users
{
    public class OtpResponse
    {
        public OtpStatus Status { get; internal set; }
    }

    public enum OtpStatus { Sent, InvalidPhoneNumber,
        TooManyRequests
    }
}