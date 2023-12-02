using Microsoft.AspNetCore.Mvc;
using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Services;
using Middagsasen.Planner.Api.Services.Users;

namespace Middagsasen.Planner.Api.Controllers
{
    [ApiController, Authorize]
    public class UsersController : ControllerBase
    {
        public UsersController(IUserService userService)
        {
            UserService = userService;
        }

        public IUserService UserService { get; }

        [HttpGet("api/me")]
        public async Task<IActionResult> Me()
        {
            var user = (UserResponse?)HttpContext.Items["User"];
            if (user == null) return Unauthorized();

            var response = await UserService.GetUserById(user.Id);
            return Ok(response);
        }

        [HttpPut("api/me")]
        public async Task<IActionResult> UpdateMe([FromBody]UserRequest request)
        {
            var user = (UserResponse?)HttpContext.Items["User"];
            if (user == null) return Unauthorized();

            var response = await UserService.UpdateUser(user.Id, request);
            if (response == null) return NotFound($"Fant ikke bruker med ID {user.Id}");

            return Ok(response);
        }

        [HttpGet("api/users/phone")]
        public async Task<IActionResult> GetPhoneList()
        {
            var response = await UserService.GetPhoneList();
            return Ok(response);
        }

        [HttpGet("api/users")]
        public async Task<IActionResult> GetUsers()
        {
            var user = (UserResponse?)HttpContext.Items["User"];
            if (user == null) return Unauthorized();

            var response = await UserService.GetUsers();
            return Ok(response);
        }
    }
}

