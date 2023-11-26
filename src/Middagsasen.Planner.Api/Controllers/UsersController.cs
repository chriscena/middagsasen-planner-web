using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Middagsasen.Planner.Api.Services;
using Middagsasen.Planner.Api.Services.Users;

namespace Middagsasen.Planner.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController, Authorize]
    public class UsersController : ControllerBase
    {
        public UsersController(IUserService userService)
        {
            UserService = userService;
        }

        public IUserService UserService { get; }

        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            var user = (UserResponse?)HttpContext.Items["User"];
            if (user == null) return Unauthorized();

            var response = await UserService.GetUserById(user.Id);
            return Ok(response);
        }

        [HttpGet("")]
        public async Task<IActionResult> GetUsers()
        {
            var user = (UserResponse?)HttpContext.Items["User"];
            if (user == null) return Unauthorized();

            var response = await UserService.GetUsers();
            return Ok(response);
        }
    }
}

