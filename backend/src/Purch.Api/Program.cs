using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Purch.Api.Endpoints;
using Purch.Api.ErrorHandling;
using Purch.Api.Middleware;
using Purch.Application.Auth;
using Purch.Application.Catalog;
using Purch.Application.Common;
using Purch.Application.Onboarding;
using Purch.Application.Pos;
using Purch.Application.Shifts;
using Purch.Infrastructure.Auth;
using Purch.Infrastructure.Deployment;
using Purch.Infrastructure.Persistence;
using Purch.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
    };
});

builder.Services.AddHttpContextAccessor();

builder.Services.AddSingleton<IDeploymentContext, ConfigDeploymentContext>();
builder.Services.AddScoped<ICurrentTenantProvider, HttpContextCurrentTenantProvider>();
builder.Services.AddScoped<ICurrentActorProvider, HttpContextCurrentActorProvider>();

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

builder.Services.AddScoped<IUnitOfWork, EfUnitOfWork>();
builder.Services.AddScoped<ITenantRepository, EfTenantRepository>();
builder.Services.AddScoped<IBranchRepository, EfBranchRepository>();
builder.Services.AddScoped<IAuditLogRepository, EfAuditLogRepository>();
builder.Services.AddScoped<IDepartmentRepository, EfDepartmentRepository>();
builder.Services.AddScoped<IBootstrapTenantService, BootstrapTenantService>();
builder.Services.AddScoped<IStaffService, StaffService>();
builder.Services.AddScoped<IBranchService, BranchService>();
builder.Services.AddScoped<IDepartmentService, DepartmentService>();
builder.Services.AddScoped<IDeviceManagementService, DeviceManagementService>();
builder.Services.AddScoped<ITenantSettingsService, TenantSettingsService>();
builder.Services.AddScoped<IAuditLogQueryService, AuditLogQueryService>();

builder.Services.AddScoped<ICategoryRepository, EfCategoryRepository>();
builder.Services.AddScoped<IItemRepository, EfItemRepository>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IItemService, ItemService>();
builder.Services.AddScoped<IModifierGroupRepository, EfModifierGroupRepository>();
builder.Services.AddScoped<IModifierGroupService, ModifierGroupService>();
builder.Services.AddScoped<IItemModifierGroupRepository, EfItemModifierGroupRepository>();
builder.Services.AddScoped<IItemModifierGroupService, ItemModifierGroupService>();
builder.Services.AddScoped<IItemBatchRepository, EfItemBatchRepository>();
builder.Services.AddScoped<IItemBatchService, ItemBatchService>();
builder.Services.AddScoped<IBundlePromoRuleRepository, EfBundlePromoRuleRepository>();
builder.Services.AddScoped<IBundlePromoRuleService, BundlePromoRuleService>();
builder.Services.AddScoped<IItemVariantRepository, EfItemVariantRepository>();
builder.Services.AddScoped<IItemVariantService, ItemVariantService>();
builder.Services.AddScoped<IItemComboComponentRepository, EfItemComboComponentRepository>();
builder.Services.AddScoped<IItemComboComponentService, ItemComboComponentService>();
builder.Services.AddScoped<ITransactionRepository, EfTransactionRepository>();
builder.Services.AddScoped<IPaymentRepository, EfPaymentRepository>();
builder.Services.AddScoped<IReceiptSequenceRepository, EfReceiptSequenceRepository>();
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<IShiftRepository, EfShiftRepository>();
builder.Services.AddScoped<IShiftService, ShiftService>();

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
            // JwtTokenService issues the role under our own claim name (JwtClaimTypes.Role),
            // not the .NET-default ClaimTypes.Role — map it here so [Authorize(Roles = "Admin")]
            // reads the right claim instead of silently never matching.
            RoleClaimType = JwtClaimTypes.Role,
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Must be first: wraps every later middleware/endpoint so any thrown exception
// (including ones from TenantResolutionMiddleware or endpoint handlers) is caught
// and turned into a consistent ProblemDetails response, never a raw 500 with no body.
app.UseExceptionHandler();

// Catches status codes set without a response body (e.g. JWT auth failing with a bare
// 401, [Authorize] failing with a bare 403, an unmatched route's default 404) and fills
// in a ProblemDetails body for those too, so no error response is ever silently empty.
app.UseStatusCodePages();

app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapAuthEndpoints();
app.MapOnboardingEndpoints();
app.MapCatalogEndpoints();
app.MapPosEndpoints();
app.MapShiftEndpoints();

app.Run();

// Exposed for WebApplicationFactory<Program> in Purch.IntegrationTests.
public partial class Program;
