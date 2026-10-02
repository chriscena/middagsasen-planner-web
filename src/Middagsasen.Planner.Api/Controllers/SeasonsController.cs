using Microsoft.AspNetCore.Mvc;
using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Services.Seasons;

namespace Middagsasen.Planner.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController, Authorize]
    public class SeasonsController : ControllerBase
    {
        public SeasonsController(ISeasonService seasonService)
        {
            SeasonService = seasonService;
        }

        public ISeasonService SeasonService { get; }

        [HttpGet]
        [ProducesResponseType<IEnumerable<SeasonResponse>>(StatusCodes.Status200OK)]
        public IEnumerable<SeasonResponse> Get()
        {
            return SeasonService.GetSeasons();
        }
    }
}
