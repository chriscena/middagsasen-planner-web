using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services.SmsSender;

namespace Middagsasen.Planner.Api.Services.System
{
    public class SystemService : ISystemService
    {
        private readonly PlannerDbContext _dbContext;
        private readonly ISmsSenderSettings _settings;

        public SystemService(PlannerDbContext dbContext, ISmsSenderSettings settings)
        {
            _dbContext = dbContext;
            _settings = settings;
        }

        public SystemDiagnosticsResponse GetDiagnostics()
        {
            var dbTest = _dbContext.Users.Count() > 0 ? "OK" : "Failed";
            var settingsTest = !string.IsNullOrEmpty(_settings.SmsSenderName) ? "OK" : "Failed";
            return new SystemDiagnosticsResponse
            {
                DatabaseConnection = dbTest,
                Settings = settingsTest
            };
        }
    }
}
