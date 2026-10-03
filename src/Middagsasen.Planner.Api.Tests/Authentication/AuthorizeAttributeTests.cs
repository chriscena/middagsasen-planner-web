using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Services;
using Middagsasen.Planner.Api.Services.Users;
using NSubstitute;

namespace Middagsasen.Planner.Api.Tests.Authentication
{
    public class AuthorizeAttributeTests
    {
        private static DefaultHttpContext CreateHttpContext(bool loggedIn, string role = Roles.User)
        {
            var context = new DefaultHttpContext();
            if (loggedIn)
            {
                context.Items["User"] = new UserResponse { Id = 1, PhoneNo = "12345678", IsAdmin = role == Roles.Administrator };
                context.User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.Role, role) }, "Password", ClaimTypes.Name, ClaimTypes.Role));
            }
            return context;
        }

        private static AuthorizationFilterContext CreateFilterContext(HttpContext httpContext)
        {
            var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
            return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
        }

        [Fact]
        public void ThrowsUnauthorized_WhenNotLoggedIn()
        {
            var filterContext = CreateFilterContext(CreateHttpContext(loggedIn: false));

            var ex = Assert.Throws<NotAuthenticatedException>(() => new AuthorizeAttribute().OnAuthorization(filterContext));
            Assert.Equal(NotAuthenticatedException.DefaultMessage, ex.Message);
        }

        [Fact]
        public void ThrowsUnauthorized_WhenNotLoggedIn_EvenIfRoleIsRequired()
        {
            var filterContext = CreateFilterContext(CreateHttpContext(loggedIn: false));

            Assert.Throws<NotAuthenticatedException>(
                () => new AuthorizeAttribute { Role = Roles.Administrator }.OnAuthorization(filterContext));
        }

        [Fact]
        public void ThrowsForbidden_WhenRoleIsMissing()
        {
            var filterContext = CreateFilterContext(CreateHttpContext(loggedIn: true, Roles.User));

            var ex = Assert.Throws<ForbiddenAccessException>(
                () => new AuthorizeAttribute { Role = Roles.Administrator }.OnAuthorization(filterContext));
            Assert.Equal(ForbiddenAccessException.DefaultMessage, ex.Message);
        }

        [Theory]
        [InlineData(null, Roles.User)]
        [InlineData(null, Roles.Administrator)]
        [InlineData(Roles.Administrator, Roles.Administrator)]
        public void Allows_WhenLoggedInWithRequiredRole(string? requiredRole, string userRole)
        {
            var filterContext = CreateFilterContext(CreateHttpContext(loggedIn: true, userRole));

            new AuthorizeAttribute { Role = requiredRole }.OnAuthorization(filterContext);

            Assert.Null(filterContext.Result);
        }

        [Theory]
        [InlineData(false, StatusCodes.Status401Unauthorized, NotAuthenticatedException.DefaultMessage)]
        [InlineData(true, StatusCodes.Status403Forbidden, ForbiddenAccessException.DefaultMessage)]
        public async Task ExceptionHandlingMiddleware_WritesProblemDetails(bool loggedIn, int expectedStatus, string expectedDetail)
        {
            var services = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
            var middleware = new ExceptionHandlingMiddleware(
                httpContext =>
                {
                    new AuthorizeAttribute { Role = Roles.Administrator }.OnAuthorization(CreateFilterContext(httpContext));
                    return Task.CompletedTask;
                },
                Substitute.For<ILogger<ExceptionHandlingMiddleware>>(),
                services.GetRequiredService<IProblemDetailsService>());

            var context = CreateHttpContext(loggedIn);
            context.RequestServices = services;
            context.Response.Body = new MemoryStream();

            await middleware.Invoke(context);

            Assert.Equal(expectedStatus, context.Response.StatusCode);
            Assert.Equal("application/problem+json", context.Response.ContentType);
            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var problem = await JsonSerializer.DeserializeAsync<ProblemDetails>(context.Response.Body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            Assert.NotNull(problem);
            Assert.Equal(expectedStatus, problem.Status);
            Assert.Equal(expectedDetail, problem.Detail);
        }
    }
}
