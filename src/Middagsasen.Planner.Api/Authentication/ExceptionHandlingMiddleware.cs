using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Middagsasen.Planner.Api.Services;

namespace Middagsasen.Planner.Api.Authentication
{
    /// <summary>
    /// Oversetter domene-exceptions til HTTP-statuskoder og skriver feilen som
    /// ProblemDetails (RFC 9457, <c>application/problem+json</c>).
    /// </summary>
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;
        private readonly IProblemDetailsService _problemDetailsService;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IProblemDetailsService problemDetailsService)
        {
            _next = next;
            _logger = logger;
            _problemDetailsService = problemDetailsService;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex) when (context.Response.HasStarted)
            {
                // Responsen er allerede startet (f.eks. under strømming av en fil), så statuskode og
                // headere kan ikke endres. Logg feilen og kast den videre slik at serveren avbryter svaret.
                _logger.LogError(ex, "Unhandled exception after the response had started");
                throw;
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var (statusCode, detail) = exception switch
            {
                EntityNotFoundException => (StatusCodes.Status404NotFound, exception.Message),
                ForbiddenAccessException => (StatusCodes.Status403Forbidden, exception.Message),
                EntityLockedException => (StatusCodes.Status409Conflict, exception.Message),
                ConcurrentUpdateException => (StatusCodes.Status409Conflict, exception.Message),
                NotAuthenticatedException => (StatusCodes.Status401Unauthorized, exception.Message),
                DomainValidationException => (StatusCodes.Status400BadRequest, exception.Message),
                // Øvrige exceptions (inkl. InvalidOperationException fra EF og UnauthorizedAccessException
                // fra I/O o.l.) er interne feil: meldingen kan inneholde interne detaljer og vises derfor
                // ikke til brukeren, og feilen logges.
                _ =>(StatusCodes.Status500InternalServerError, "Det oppstod en uventet feil."),
            };

            if (statusCode == StatusCodes.Status500InternalServerError)
            {
                _logger.LogError(exception, "Unhandled exception");
            }

            context.Response.StatusCode = statusCode;

            var problemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = ReasonPhrases.GetReasonPhrase(statusCode),
                Detail = detail,
            };

            var written = await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails = problemDetails,
                Exception = exception,
            });

            // Ingen writer kunne skrive (f.eks. pga. Accept-header) — skriv ProblemDetails direkte.
            if (!written)
            {
                await context.Response.WriteAsJsonAsync(problemDetails, options: null, contentType: "application/problem+json");
            }
        }
    }
}
