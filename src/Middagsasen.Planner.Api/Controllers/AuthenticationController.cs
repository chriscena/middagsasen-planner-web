using Microsoft.AspNetCore.Mvc;
using Middagsasen.Planner.Api.Services.Authentication;
using System.Security.Claims;

namespace Middagsasen.Planner.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthenticationController : ControllerBase
    {
        internal const string InvalidPhoneNumberMessage = "Ugyldig telefonnummer";
        internal const string AuthenticationFailedMessage = "Feil telefonnummer eller engangskode.";
        internal const string TooManyRequestsMessage = "For mange forsøk. Vent litt før du ber om en ny engangskode.";
        internal const string SessionNotFoundMessage = "Fant ingen aktiv innlogging å logge ut.";

        public AuthenticationController(IAuthenticationService authService)
        {
            AuthService = authService;
        }

        public IAuthenticationService AuthService { get; }

        [HttpPost("authenticate")]
        [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Authenticate(AuthRequest request)
        {
            var response = await AuthService.Authenticate(request);
            switch (response.Status)
            {
                case AuthStatus.Success:
                    return Ok(response);
                case AuthStatus.InvalidUsername:
                    return Problem(detail: InvalidPhoneNumberMessage, statusCode: StatusCodes.Status400BadRequest);
                case AuthStatus.AuthenticationFailed:
                default:
                    return Problem(detail: AuthenticationFailedMessage, statusCode: StatusCodes.Status401Unauthorized);
            }
        }

        [HttpPost("otp")]
        [ProducesResponseType(typeof(OtpResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> CreateOneTimePassword(OtpRequest request)
        {
            var response = await AuthService.GenerateOtpForUser(request);
            switch (response.Status)
            {
                case OtpStatus.Sent:
                    return Ok(response);
                case OtpStatus.InvalidPhoneNumber:
                    return Problem(detail: InvalidPhoneNumberMessage, statusCode: StatusCodes.Status400BadRequest);
                case OtpStatus.TooManyRequests:
                default:
                    return Problem(detail: TooManyRequestsMessage, statusCode: StatusCodes.Status429TooManyRequests);
            }
        }

        [HttpPost("logout")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> LogOut()
        {
            var user = HttpContext.User;
            var sessionIdString = user.FindFirstValue(ClaimTypes.Authentication);
            if (sessionIdString == null)
                return Problem(detail: SessionNotFoundMessage, statusCode: StatusCodes.Status404NotFound);

            await AuthService.LogOut(Guid.Parse(sessionIdString));
            return Ok();
        }
    }
}
