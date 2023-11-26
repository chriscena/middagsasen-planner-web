using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Middagsasen.Planner.Api;
using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services;
using Middagsasen.Planner.Api.Services.Authentication;
using Middagsasen.Planner.Api.Services.SmsSender;
using Middagsasen.Planner.Api.Services.Users;



var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors();

builder.Services.Configure<InfrastructureSettings>(builder.Configuration.GetSection("Infrastructure"));
builder.Services.AddTransient<ISmsSenderSettings>(serviceProvider => serviceProvider.GetService<IOptions<InfrastructureSettings>>()?.Value);
builder.Services.AddTransient<IAuthSettings>(serviceProvider => serviceProvider.GetService<IOptions<InfrastructureSettings>>()?.Value);
builder.Services.AddDbContext<PlannerDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddTransient<ISmsSender, SmsSenderService>();

builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IUserService, UserService>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(x => x
        .AllowAnyOrigin()
        .AllowAnyMethod()
        .AllowAnyHeader());

// custom jwt auth middleware
app.UseMiddleware<JwtMiddleware>();
app.MapControllers();

app.UseHttpsRedirection();

app.Run();
