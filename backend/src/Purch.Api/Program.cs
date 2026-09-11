using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Purch.Api.Endpoints;
using Purch.Api.Middleware;
using Purch.Application.Auth;
using Purch.Infrastructure.Auth;
using Purch.Infrastructure.Deployment;
using Purch.Infrastructure.Persistence;
using Purch.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();

builder.Services.AddSingleton<IDeploymentContext, ConfigDeploymentContext>();
builder.Services.AddScoped<ICurrentTenantProvider, HttpContextCurrentTenantProvider>();

builder.Services.AddDbContext<PurchDbContext>((serviceProvider, options) =>
{
    var deploymentContext = serviceProvider.GetRequiredService<IDeploymentContext>();
    _ = options.UseNpgsql(deploymentContext.DatabaseConnectionString);
});

builder.Services.AddSingleton<IPinHasher, BCryptPinHasher>();
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IDeviceRepository, EfDeviceRepository>();
builder.Services.AddScoped<IUserRepository, EfUserRepository>();
builder.Services.AddScoped<ILoginService, LoginService>();

var jwtSigningKey = builder.Configuration["JWT_SIGNING_KEY"] ?? "development-only-signing-key-change-me";
var jwtIssuer = builder.Configuration["JWT_ISSUER"] ?? "purch.io";

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtIssuer,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
            ValidateLifetime = true,
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapAuthEndpoints();

app.Run();

// Exposed for WebApplicationFactory<Program> in Purch.IntegrationTests.
public partial class Program;
