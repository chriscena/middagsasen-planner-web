using Microsoft.AspNetCore.Mvc;
using Middagsasen.Planner.Api.Services;
using Middagsasen.Planner.Api.Services.Authentication;

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
    }
}
