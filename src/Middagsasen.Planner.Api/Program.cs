using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.ApplicationInsights;
using Microsoft.Extensions.Options;
using Middagsasen.Planner.Api;
using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services.Authentication;
using Middagsasen.Planner.Api.Services.Events;
using Middagsasen.Planner.Api.Services.ResourceTypes;
using Middagsasen.Planner.Api.Services.SmsSender;
using Middagsasen.Planner.Api.Services.Storage;
using Middagsasen.Planner.Api.Services.Users;
using Middagsasen.Planner.Api.Services.Weather;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddApplicationInsights();
builder.Logging.AddFilter<ApplicationInsightsLoggerProvider>("PlannerApi", LogLevel.Trace);

builder.Services.AddApplicationInsightsTelemetry();
builder.Services.AddSerilog((services, lc) => lc
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
#if DEBUG
    .WriteTo.Console()
#endif
    .WriteTo.ApplicationInsights(
        services.GetRequiredService<TelemetryConfiguration>(),
        TelemetryConverter.Events)
    );
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors();

builder.Services.Configure<InfrastructureSettings>(settings =>
{
    settings.Secret = builder.Configuration["Infrastructure:Secret"] ?? string.Empty;
    settings.SmsUsername = builder.Configuration["Infrastructure:SmsUsername"];
    settings.SmsPassword = builder.Configuration["Infrastructure:SmsPassword"];
    settings.SmsSenderName = builder.Configuration["Infrastructure:SmsSenderName"];
    settings.ConnectionString = builder.Configuration["Infrastructure:StorageConnectionString"];
    settings.Container = builder.Configuration["Infrastructure:StorageContainer"];
});
builder.Services.AddTransient<ISmsSenderSettings>(serviceProvider => serviceProvider.GetService<IOptions<InfrastructureSettings>>()?.Value);
builder.Services.AddTransient<IAuthSettings>(serviceProvider => serviceProvider.GetService<IOptions<InfrastructureSettings>>()?.Value);
builder.Services.AddTransient<IBlobStorageSettings>(serviceProvider => serviceProvider.GetService<IOptions<InfrastructureSettings>>()?.Value);
builder.Services.AddDbContext<PlannerDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddTransient<ISmsSender, SmsSenderService>();
builder.Services.AddTransient<IStorageService, BlobStorageService>();

builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IEventsService, EventsService>();
builder.Services.AddScoped<IResourceTypesService, EventsService>();
builder.Services.AddScoped<IEventTemplatesService, EventsService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<WeatherService>();

//builder.Services.AddHostedService<WeatherDataCollector>();


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
