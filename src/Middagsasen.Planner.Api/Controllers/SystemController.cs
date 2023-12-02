using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services.SmsSender;

namespace Middagsasen.Planner.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SystemController : ControllerBase
    {
        public SystemController(PlannerDbContext dbContext, ISmsSenderSettings settings)
        {
            DbContext = dbContext;
            Settings = settings;
        }

        public PlannerDbContext DbContext { get; }
        public ISmsSenderSettings Settings { get; }

        [HttpGet]
        public ActionResult Get()
        {
            DbContext.Users.Count();
            return Ok(new { Settings.SmsSenderName});
        }
    }
}
