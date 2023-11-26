using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Services.SmsSender;

namespace Middagsasen.Planner.Api
{
    public class InfrastructureSettings : IAuthSettings, ISmsSenderSettings
    {
        public string Secret { get; set; } = null!;
        public string? SmsUsername { get; set; }
        public string? SmsPassword { get; set; }
        public string? SmsSenderName { get; set; }
        public string? SmsDeliveryReportUrl { get; set; }
    }
}
