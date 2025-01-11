namespace Middagsasen.Planner.Api.Services.Authentication
{
    public class OtpResponse
    {
        public OtpStatus Status { get; internal set; }
    }

    public enum OtpStatus
    {
        Sent, InvalidPhoneNumber,
        TooManyRequests
    }
}