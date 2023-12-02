using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Middagsasen.Planner.Api;
using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services;
using Middagsasen.Planner.Api.Services.Authentication;
using Middagsasen.Planner.Api.Services.Events;
using Middagsasen.Planner.Api.Services.SmsSender;
using Middagsasen.Planner.Api.Services.Users;



var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors();
builder.Services.AddApplicationInsightsTelemetry(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]);

builder.Services.Configure<InfrastructureSettings>(builder.Configuration.GetSection("Infrastructure"));
builder.Services.AddTransient<ISmsSenderSettings>(serviceProvider => serviceProvider.GetService<IOptions<InfrastructureSettings>>()?.Value);
builder.Services.AddTransient<IAuthSettings>(serviceProvider => serviceProvider.GetService<IOptions<InfrastructureSettings>>()?.Value);
builder.Services.AddDbContext<PlannerDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddTransient<ISmsSender, SmsSenderService>();

builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IEventsService, EventsService>();
builder.Services.AddScoped<IResourceTypesService, EventsService>();
builder.Services.AddScoped<IUserService, UserService>();


var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors(x => x
        .AllowAnyOrigin()
        .AllowAnyMethod()
        .AllowAnyHeader());

app.UseMiddleware<JwtMiddleware>();
app.MapControllers();

app.Run();
