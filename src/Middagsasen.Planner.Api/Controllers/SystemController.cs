using Microsoft.AspNetCore.Mvc;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services.SmsSender;

namespace Middagsasen.Planner.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SystemController : ControllerBase
    {
        public SystemController(ILogger<SystemController> logger, PlannerDbContext dbContext, ISmsSenderSettings settings)
        {
            Logger = logger;
            DbContext = dbContext;
            Settings = settings;
        }

        public ILogger<SystemController> Logger { get; }
        public PlannerDbContext DbContext { get; }
        public ISmsSenderSettings Settings { get; }

        [HttpGet]
        public IActionResult Get()
        {
            Logger.LogInformation("Checking configuration");
            var dbTest = DbContext.Users.Count() > 0 ? "OK" : "Failed";
            var settingsTest = !string.IsNullOrEmpty(Settings.SmsSenderName) ? "OK" : "Failed";
            return Ok(new { DatabaseConnection = dbTest, Settings = settingsTest });
        }
    }
}
