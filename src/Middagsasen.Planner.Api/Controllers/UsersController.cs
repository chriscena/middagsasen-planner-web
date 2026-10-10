using Microsoft.AspNetCore.Mvc;
using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Services.Users;

namespace Middagsasen.Planner.Api.Controllers
{
    [ApiController, Authorize]
    public class UsersController : ControllerBase
    {
        public UsersController(IUserService userService, ICurrentUserService currentUser)
        {
            UserService = userService;
            CurrentUser = currentUser;
        }

        public IUserService UserService { get; }
        public ICurrentUserService CurrentUser { get; }

        [HttpGet("api/me")]
        [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<UserResponse> Me()
        {
            return await UserService.GetUserById(CurrentUser.UserId);
        }

        [HttpPut("api/me")]
        [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        // 403 når en bruker som ikke er admin prøver å endre bemanningsvarsel (StaffingAlerts).
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<UserResponse> UpdateMe([FromBody]UpdateMeRequest request)
        {
            return await UserService.UpdateMe(CurrentUser.UserId, request);
        }

        [HttpGet("api/users/phone")]
        [ProducesResponseType(typeof(IEnumerable<PhoneResponse>), StatusCodes.Status200OK)]
        public async Task<IEnumerable<PhoneResponse>> GetPhoneList()
        {
            return await UserService.GetPhoneList();
        }

        [HttpGet("api/users")]
        [Authorize(Role = Roles.Administrator)]
        [ProducesResponseType(typeof(IEnumerable<UserResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IEnumerable<UserResponse>> GetUsers()
        {
            return await UserService.GetUsers();
        }

        [HttpGet("api/users/{id}")]
        [Authorize(Role = Roles.Administrator)]
        [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<UserResponse> GetUser(int id)
        {
            return await UserService.GetUserById(id);
        }

        [HttpPost("api/users")]
        [Authorize(Role = Roles.Administrator)]
        [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> CreateUser(UserRequest user)
        {
            var response = await UserService.Create(user);
            return Created($"/api/users/{response.Id}", response);
        }

        [HttpPut("api/users/{id}")]
        [Authorize(Role = Roles.Administrator)]
        [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<UserResponse> UpdateUser(int id, UserRequest user)
        {
            return await UserService.Update(id, user);
        }

        [HttpDelete("api/users/{id}")]
        [Authorize(Role = Roles.Administrator)]
        [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<UserResponse> DeleteUser(int id)
        {
            return await UserService.Delete(id);
        }

        [HttpGet("api/halloffame")]
        [ProducesResponseType(typeof(HallOfFameResponse), StatusCodes.Status200OK)]
        public async Task<HallOfFameResponse> GetHallOfFame()
        {
            return await UserService.GetHallOfFame();
        }
    }
}

