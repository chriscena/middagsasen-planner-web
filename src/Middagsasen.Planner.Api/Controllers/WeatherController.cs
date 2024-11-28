using Microsoft.AspNetCore.Mvc;
using Middagsasen.Planner.Api.Services.Weather;

namespace Middagsasen.Planner.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WeatherController(WeatherService weatherService) : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] LocationMeasurementRequest request)
        {
            var response = await weatherService.GetLocationMeasurements(request);

            return Ok(response);
        }

    }
}
