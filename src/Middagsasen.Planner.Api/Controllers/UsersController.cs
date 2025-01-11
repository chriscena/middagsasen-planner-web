using Microsoft.AspNetCore.Mvc;
using Middagsasen.Planner.Api.Authentication;
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
        [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> Me()
        {
            var user = (UserResponse?)HttpContext.Items["User"];
            if (user == null) return Unauthorized();

            var response = await UserService.GetUserById(user.Id);
            return Ok(response);
        }

        [HttpPut("api/me")]
        [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> UpdateMe([FromBody]UserRequest request)
        {
            var user = (UserResponse?)HttpContext.Items["User"];
            if (user == null) return Unauthorized();

            var response = await UserService.Update(user.Id, request);
            if (response == null) return NotFound($"Fant ikke bruker med ID {user.Id}");

            return Ok(response);
        }

        [HttpGet("api/users/phone")]
        [ProducesResponseType(typeof(PhoneResponse), 200)]
        public async Task<IActionResult> GetPhoneList()
        {
            var response = await UserService.GetPhoneList();
            return Ok(response);
        }

        [HttpGet("api/users")]
        [Authorize(Role = Roles.Administrator)]
        [ProducesResponseType(typeof(IEnumerable<UserResponse>), 200)]
        public async Task<IActionResult> GetUsers()
        {
            var response = await UserService.GetUsers();
            return Ok(response);
        }

        [HttpGet("api/users/{id}")]
        [Authorize(Role = Roles.Administrator)]
        [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetUser(int id)
        {
            var response = await UserService.GetUserById(id);
            if (response == null) return NotFound($"Fant ikke bruker med ID {id}");

            return Ok(response);
        }

        [HttpPost("api/users")]
        [Authorize(Role = Roles.Administrator)]
        [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
        public async Task<IActionResult> CreateUser(UserRequest user)
        {
            var response = await UserService.Create(user);
            return Created($"/api/users/{response.Id}", response);
        }

        [HttpPut("api/users/{id}")]
        [Authorize(Role = Roles.Administrator)]
        [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> UpdateUser(int id, UserRequest user)
        {
            var response = await UserService.Update(id, user);
            if (response == null) return NotFound($"Fant ikke bruker med ID {id}");
            return Ok(response);
        }

        [HttpDelete("api/users/{id}")]
        [Authorize(Role = Roles.Administrator)]
        [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var response = await UserService.Delete(id);
            if (response == null) return NotFound($"Fant ikke bruker med ID {id}");
            return Ok(response);
        }

        [HttpGet("api/halloffame")]
        [ProducesResponseType(typeof(HallOfFameResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetHallOfFame()
        {
            var response = await UserService.GetHallOfFame();
            return Ok(response);
        }
    }
}

