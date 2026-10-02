using Microsoft.AspNetCore.Mvc;
using Middagsasen.Planner.Api.Services.Weather;

namespace Middagsasen.Planner.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WeatherController(WeatherService weatherService) : ControllerBase
    {
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<LocationMeasurementResponse>), StatusCodes.Status200OK)]
        public async Task<IEnumerable<LocationMeasurementResponse>> Get([FromQuery] LocationMeasurementRequest request)
        {
            return await weatherService.GetLocationMeasurements(request);
        }

    }
}
