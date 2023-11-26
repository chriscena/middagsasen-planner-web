namespace Middagsasen.Planner.Api.Services.SmsSender
{
    public interface ISmsSenderSettings
    {
        public string? SmsUsername { get; set; }
        public string? SmsPassword { get; set; }
        public string? SmsSenderName { get; set; }
        public string? SmsDeliveryReportUrl { get; set; }
    }

    public interface ISmsSender
    {
        Task<SmsResult> SendMessages(IEnumerable<SmsMessage> messages);
    }

    public class SmsMessage
    {
        public Guid SmsNotificationId { get; set; }
        public long ReceiverPhoneNo { get; set; }
        public string Body { get; set; }
    }

    public class SmsResult
    {
        public bool Success { get; set; }
        public string? Info { get; set; }
        public IEnumerable<SmsMessageResult> Messages { get; set; }
    }

    public class SmsMessageResult
    {
        public long ReceiverPhoneNo { get; set; }
        public bool Success { get; set; }
        public string ExternalId { get; set; }
        public string Info { get; set; }
    }
}