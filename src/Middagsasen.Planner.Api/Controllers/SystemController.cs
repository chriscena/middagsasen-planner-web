using Microsoft.AspNetCore.Mvc;
using Middagsasen.Planner.Api.Services.System;

namespace Middagsasen.Planner.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SystemController : ControllerBase
    {
        public SystemController(ISystemService systemService)
        {
            SystemService = systemService;
        }

        public ISystemService SystemService { get; }

        [HttpGet]
        public IActionResult Get()
        {
            return Ok(SystemService.GetDiagnostics());
        }
    }
}
