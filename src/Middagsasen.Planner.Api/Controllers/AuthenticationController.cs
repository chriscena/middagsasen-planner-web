using Microsoft.AspNetCore.Mvc;
using Middagsasen.Planner.Api.Services.Authentication;
using System.Security.Claims;

namespace Middagsasen.Planner.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthenticationController : ControllerBase
    {
        public AuthenticationController(IAuthenticationService authService)
        {
            AuthService = authService;
        }

        public IAuthenticationService AuthService { get; }



        [HttpPost("authenticate")]
        public async Task<IActionResult> Authenticate(AuthRequest request)
        {
            var response = await AuthService.Authenticate(request);
            switch (response.Status)
            {
                case AuthStatus.Success:
                    return Ok(response);
                case AuthStatus.InvalidUsername:
                    return BadRequest("Ugyldig telefonnummer");
                case AuthStatus.AuthenticationFailed:
                default:
                    return Unauthorized();
            }
        }

        [HttpPost("otp")]
        public async Task<IActionResult> CreateOneTimePassword(OtpRequest request)
        {
            var response = await AuthService.GenerateOtpForUser(request);
            switch (response.Status)
            {
                case OtpStatus.Sent:
                    return Ok(response);
                case OtpStatus.InvalidPhoneNumber:
                    return BadRequest("Ugyldig telefonnummer");
                case OtpStatus.TooManyRequests:
                default:
                    return new StatusCodeResult(StatusCodes.Status429TooManyRequests);
            }
        }

        [HttpPost("logout")]
        public async Task<IActionResult> LogOut()
        {
            var user = HttpContext.User;
            var sessionIdString = user.FindFirstValue(ClaimTypes.Authentication);
            if (sessionIdString == null) return NotFound();
            
            await AuthService.LogOut(Guid.Parse(sessionIdString));
            return Ok();
        }
    }
}
