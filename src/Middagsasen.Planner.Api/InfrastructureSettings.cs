using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Services.SmsSender;
using Middagsasen.Planner.Api.Services.Storage;

namespace Middagsasen.Planner.Api
{
    public class InfrastructureSettings : IAuthSettings, ISmsSenderSettings, IBlobStorageSettings
    {
        public string Secret { get; set; } = null!;
        public string? SmsUsername { get; set; }
        public string? SmsPassword { get; set; }
        public string? SmsSenderName { get; set; }
        public string? SmsDeliveryReportUrl { get; set; }
        public string? ConnectionString { get; set; }
        public string? Container { get; set; }
    }
}
