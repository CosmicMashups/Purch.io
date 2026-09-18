using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Purch.Api.Endpoints;
using Purch.Api.ErrorHandling;
using Purch.Api.Middleware;
using Purch.Api.RateLimiting;
using Purch.Application.Auth;
using Purch.Application.Catalog;
using Purch.Application.Common;
using Purch.Application.CreditLedger;
using Purch.Application.Onboarding;
using Purch.Application.Inventory;
using Purch.Application.Devices;
using Purch.Application.Kiosk;
using Purch.Application.Pos;
using Purch.Application.Promotions;
using Purch.Application.Reporting;
using Purch.Application.Shifts;
using Purch.Application.Sync;
using Purch.Domain.Enums;
using Purch.Infrastructure.Auth;
using Purch.Infrastructure.Deployment;
using Purch.Infrastructure.Persistence;
using Purch.Infrastructure.Repositories;
using Purch.Infrastructure.Storage;

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

builder.Services.AddHttpClient(nameof(SupabaseFileStorage));
builder.Services.AddSingleton<IFileStorage>(serviceProvider =>
{
    var deploymentContext = serviceProvider.GetRequiredService<IDeploymentContext>();
    if (deploymentContext.Mode == DeploymentMode.Local)
    {
        var env = serviceProvider.GetRequiredService<IWebHostEnvironment>();
        var webRootPath = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        return new LocalFileStorage(webRootPath);
    }

    var httpClient = serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(SupabaseFileStorage));
    return new SupabaseFileStorage(
        httpClient,
        deploymentContext.StorageLocation,
        deploymentContext.StorageKey!,
        deploymentContext.StorageBucket!);
});
builder.Services.AddScoped<ICurrentTenantProvider, HttpContextCurrentTenantProvider>();
builder.Services.AddScoped<ICurrentActorProvider, HttpContextCurrentActorProvider>();

builder.Services.AddDbContext<PurchDbContext>((serviceProvider, options) =>
{
    var deploymentContext = serviceProvider.GetRequiredService<IDeploymentContext>();
    _ = options.UseNpgsql(deploymentContext.DatabaseConnectionString);
});

builder.Services.AddSingleton<IPinHasher, BCryptPinHasher>();
builder.Services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IDeviceRepository, EfDeviceRepository>();
builder.Services.AddScoped<IUserRepository, EfUserRepository>();
builder.Services.AddScoped<ILoginService, LoginService>();
builder.Services.AddScoped<IRefreshTokenRepository, EfRefreshTokenRepository>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();
builder.Services.AddScoped<ITokenRefreshService, TokenRefreshService>();
builder.Services.AddScoped<IPasswordResetTokenRepository, EfPasswordResetTokenRepository>();
builder.Services.AddSingleton<IPasswordResetTokenNotifier, ConsolePasswordResetTokenNotifier>();
builder.Services.AddScoped<IPasswordResetService, PasswordResetService>();

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
builder.Services.AddScoped<IKioskPrepSequenceRepository, EfKioskPrepSequenceRepository>();
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<IKioskSessionService, KioskSessionService>();
builder.Services.AddScoped<IUnattendedSessionService, UnattendedSessionService>();
builder.Services.AddScoped<IShiftRepository, EfShiftRepository>();
builder.Services.AddScoped<IShiftService, ShiftService>();
builder.Services.AddScoped<IPromoCodeRepository, EfPromoCodeRepository>();
builder.Services.AddScoped<IPromoCodeService, PromoCodeService>();
builder.Services.AddScoped<IBogoPromoRuleRepository, EfBogoPromoRuleRepository>();
builder.Services.AddScoped<IBogoPromoRuleService, BogoPromoRuleService>();
builder.Services.AddScoped<IComboPromoRuleRepository, EfComboPromoRuleRepository>();
builder.Services.AddScoped<IComboPromoRuleService, ComboPromoRuleService>();
builder.Services.AddScoped<IItemDiscountPromoRuleRepository, EfItemDiscountPromoRuleRepository>();
builder.Services.AddScoped<IItemDiscountPromoRuleService, ItemDiscountPromoRuleService>();
builder.Services.AddScoped<IBirReadingService, BirReadingService>();
builder.Services.AddScoped<IReportingRepository, EfReportingRepository>();
builder.Services.AddScoped<IReportScopeResolver, ReportScopeResolver>();
builder.Services.AddScoped<ISalesDashboardService, SalesDashboardService>();
builder.Services.AddScoped<IInventoryReportService, InventoryReportService>();
builder.Services.AddScoped<IStaffPerformanceService, StaffPerformanceService>();
builder.Services.AddScoped<IDepartmentSalesReportService, DepartmentSalesReportService>();
builder.Services.AddScoped<ICustomerCreditLedgerRepository, EfCustomerCreditLedgerRepository>();
builder.Services.AddScoped<ICustomerCreditLedgerService, CustomerCreditLedgerService>();
builder.Services.AddScoped<IInventoryMovementRepository, EfInventoryMovementRepository>();
builder.Services.AddScoped<IInventoryMovementService, InventoryMovementService>();
builder.Services.AddScoped<IInventoryDashboardService, InventoryDashboardService>();
builder.Services.AddScoped<IBranchTransferRepository, EfBranchTransferRepository>();
builder.Services.AddScoped<IBranchTransferService, BranchTransferService>();
builder.Services.AddScoped<ISupplierRepository, EfSupplierRepository>();
builder.Services.AddScoped<ISupplierService, SupplierService>();
builder.Services.AddScoped<IPurchaseOrderRepository, EfPurchaseOrderRepository>();
builder.Services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
builder.Services.AddScoped<IInventoryItemRepository, EfInventoryItemRepository>();
builder.Services.AddScoped<IInventoryItemService, InventoryItemService>();
builder.Services.AddScoped<IItemRecipeRepository, EfItemRecipeRepository>();
builder.Services.AddScoped<IItemRecipeService, ItemRecipeService>();
builder.Services.AddScoped<ISyncedRecordRepository, EfSyncedRecordRepository>();
builder.Services.AddScoped<ISyncService, SyncService>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Read lazily (not into locals above builder.Build()): WebApplicationFactory
        // splices its test config overrides into builder.Configuration at Build()-time,
        // so capturing these values any earlier reads stale defaults during tests while
        // JwtTokenService (DI-injected IConfiguration, read post-Build) signs with the
        // real values — a signing-key/issuer mismatch that 401s every authenticated call.
        var jwtSigningKey = builder.Configuration["JWT_SIGNING_KEY"] ?? "development-only-signing-key-change-me";
        var jwtIssuer = builder.Configuration["JWT_ISSUER"] ?? "purch.io";

        // JwtSecurityTokenHandler otherwise remaps short claim names it recognizes
        // (like "role") to legacy long-form XML-namespace URIs before the
        // ClaimsIdentity is built, so no claim literally named "role" would exist
        // for RoleClaimType below to match — every RequireRole() check would 403.
        options.MapInboundClaims = false;

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

// Throttles the anonymous credential-guessing surfaces (login, kiosk pairing,
// password-reset request/confirm) — partitioned per client IP so one abusive caller
// can't exhaust another's budget. A fixed window rather than sliding/token-bucket:
// simplest option that still bounds guesses/minute, and these endpoints don't need
// smoother burst handling.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy(RateLimiterPolicies.AuthSensitive, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(15),
                PermitLimit = 10,
                QueueLimit = 0,
            }));
});

var app = builder.Build();

// Render's preDeployCommand (see render.yaml) runs this exact published
// image with an extra "migrate" argument before the new deploy goes live —
// there's no SDK/dotnet-ef in the runtime image (see Dockerfile), so this
// reuses the app's own already-wired DbContext/connection string instead of
// requiring a second toolchain. Exits immediately after, never starting the
// web server, so a migration failure fails the deploy instead of serving
// traffic against a stale/half-migrated schema.
if (args is ["migrate"])
{
    using var migrateScope = app.Services.CreateScope();
    var migrateDbContext = migrateScope.ServiceProvider.GetRequiredService<PurchDbContext>();
    await migrateDbContext.Database.MigrateAsync();
    return;
}

// Local/on-prem installs have no separate CI/CD migration step — a store
// owner running the installer shouldn't need the EF Core CLI, so a
// Local-mode instance migrates its own database once at startup instead;
// Cloud's migrations run via the "migrate" preDeployCommand above.
using (var startupScope = app.Services.CreateScope())
{
    var deploymentContext = startupScope.ServiceProvider.GetRequiredService<IDeploymentContext>();
    var dbContext = startupScope.ServiceProvider.GetRequiredService<PurchDbContext>();

    if (deploymentContext.Mode == DeploymentMode.Local)
    {
        await dbContext.Database.MigrateAsync();
    }

    // Every table has Row Level Security enabled with no policies (see the
    // EnableRowLevelSecurity migration) — deliberately, since this app never
    // uses Supabase Auth/PostgREST and this backend is the only thing that
    // should ever touch these rows. That's only safe because this connection
    // always authenticates as a role that bypasses RLS (Cloud/Supabase's
    // postgres role, or Local mode's Postgres superuser). If a future
    // connection string ever used a lower-privileged role instead, every
    // read would silently return zero rows and every write would fail with
    // an opaque RLS-violation error — while JWT auth kept working fine,
    // making it look like a data bug rather than a role misconfiguration.
    // Fail loudly at startup instead of letting that happen silently.
    var bypassesRls = await dbContext.Database
        .SqlQueryRaw<bool>("SELECT rolbypassrls AS \"Value\" FROM pg_roles WHERE rolname = current_user")
        .SingleAsync();

    if (!bypassesRls)
    {
        throw new InvalidOperationException(
            "This backend's database connection does not bypass Row Level Security — its role lacks " +
            "BYPASSRLS/superuser. Every table has RLS enabled with no policies, so a non-bypassing connection " +
            "would silently return zero rows on every read and fail every write. Reconnect using a role with " +
            "BYPASSRLS (Supabase's postgres role, or the Postgres superuser in Local mode) before starting the app.");
    }
}

// Must be first: wraps every later middleware/endpoint so any thrown exception
// (including ones from TenantResolutionMiddleware or endpoint handlers) is caught
// and turned into a consistent ProblemDetails response, never a raw 500 with no body.
app.UseExceptionHandler();

// Catches status codes set without a response body (e.g. JWT auth failing with a bare
// 401, [Authorize] failing with a bare 403, an unmatched route's default 404) and fills
// in a ProblemDetails body for those too, so no error response is ever silently empty.
app.UseStatusCodePages();

var webRootPath = app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");
if (!Directory.Exists(webRootPath))
{
    Directory.CreateDirectory(webRootPath);
}
var defaultUploadsPath = Path.Combine(webRootPath, "uploads");
if (!Directory.Exists(defaultUploadsPath))
{
    Directory.CreateDirectory(defaultUploadsPath);
}

app.UseStaticFiles();

app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapAuthEndpoints();
app.MapOnboardingEndpoints();
app.MapCatalogEndpoints();
app.MapPosEndpoints();
app.MapKioskEndpoints();
app.MapDeviceDisplayEndpoints();
app.MapShiftEndpoints();
app.MapPromoCodeEndpoints();
app.MapReportingEndpoints();
app.MapCreditLedgerEndpoints();
app.MapInventoryEndpoints();
app.MapSyncEndpoints();
app.MapUploadEndpoints();

app.Run();

// Exposed for WebApplicationFactory<Program> in Purch.IntegrationTests.
public partial class Program;
