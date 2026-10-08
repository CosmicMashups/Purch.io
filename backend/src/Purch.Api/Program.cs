using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Purch.Api;
using Purch.Api.Endpoints;
using Purch.Api.ErrorHandling;
using Purch.Api.Health;
using Purch.Api.Middleware;
using Purch.Api.RateLimiting;
using Purch.Application.Approvals;
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
using Purch.Infrastructure.Retention;
using Purch.Infrastructure.Storage;

var builder = WebApplication.CreateBuilder(args);

// Render's Dockerfile resolves this via a shell entrypoint
// (ASPNETCORE_HTTP_PORTS=${PORT:-8080}) instead, but Vercel's chiseled
// container runtime (see Dockerfile.vercel) ships with no shell at all, so
// any ENTRYPOINT that needs one fails to start before .NET even runs — no
// app-level exception, no log line, nothing. Binding Kestrel explicitly here
// means the entrypoint can be plain `dotnet Purch.Api.dll` on both hosts.
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// The catalog and report payloads are large, repetitive JSON; on a store connection this is the cheapest
// bandwidth win there is. The API authenticates with a bearer header, not cookies, so there is no
// BREACH-style secret reflected into compressed bodies.
builder.Services.AddResponseCompression(options => options.EnableForHttps = true);
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
    };
});

builder.Services.AddHttpContextAccessor();

// Readiness (is the database reachable?) is tagged so /health/ready runs it while /health stays a pure
// liveness check. See DatabaseHealthCheck.
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

// On a deploy or scale-down the host sends SIGTERM; give in-flight requests (a checkout mid-save) time to
// finish instead of the .NET default of 30s being an accident of the framework.
builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(30));

// Periodically purges stale refresh/password-reset tokens and synced-record idempotency rows; audit log and
// inventory movement purging stays off unless a retention period is explicitly configured — see RetentionOptions.
builder.Services.Configure<RetentionOptions>(builder.Configuration.GetSection(RetentionOptions.SectionName));
builder.Services.AddScoped<RetentionSweeper>();
builder.Services.AddHostedService<RetentionHostedService>();

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
    // Transient-failure retries cover the brief connection drops a pooled Supabase/Vercel
    // connection sees after idle periods; nothing here opens manual transactions, so the
    // retrying execution strategy is safe (SaveChanges is already atomic per call).
    _ = options.UseNpgsql(
        deploymentContext.DatabaseConnectionString,
        npgsql => npgsql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(4), errorCodesToAdd: null));
});

builder.Services.AddSingleton<IPinHasher, BCryptPinHasher>();
builder.Services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddSingleton<ISupervisorAttestationService, SupervisorAttestationService>();

// Email and password sign-in. Cloud mode checks the password with Supabase Auth; Local mode keeps a credential table in
// our own database. Permissions always come from our own Membership rows, never from the provider. Setting
// PURCH_IDENTITY_PROVIDER=Local keeps passwords in our own database in Cloud mode too; that is for the end-to-end test
// stack, which has no Supabase project, and is never set in a real deployment.
var useLocalIdentity = string.Equals(builder.Configuration["PURCH_IDENTITY_PROVIDER"], "Local", StringComparison.OrdinalIgnoreCase);
// Fail closed: this switch bypasses Supabase and checks passwords against our own table. A Production cloud
// deployment that has it set (a copied e2e environment, a stray variable) must refuse to start, not quietly
// run with a weaker identity store.
if (useLocalIdentity
    && builder.Environment.IsProduction()
    && !string.Equals(builder.Configuration["PURCH_DEPLOYMENT_MODE"], "Local", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException(
        "PURCH_IDENTITY_PROVIDER=Local is only for the end-to-end test stack and cannot be used by a Production cloud deployment.");
}

builder.Services.AddHttpClient(SupabaseIdentityProvider.HttpClientName);
builder.Services.AddScoped<IIdentityProvider>(serviceProvider =>
    useLocalIdentity || serviceProvider.GetRequiredService<IDeploymentContext>().Mode == DeploymentMode.Local
        ? ActivatorUtilities.CreateInstance<LocalIdentityProvider>(serviceProvider)
        : new SupabaseIdentityProvider(
            serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(SupabaseIdentityProvider.HttpClientName),
            serviceProvider.GetRequiredService<IDeploymentContext>()));
builder.Services.AddScoped<IAccountRepository, EfAccountRepository>();
builder.Services.AddScoped<ISignInThrottleRepository, EfSignInThrottleRepository>();
builder.Services.AddScoped<LegacyMigration>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IMembershipRepository, EfMembershipRepository>();
builder.Services.AddScoped<IEnrolmentInviteRepository, EfEnrolmentInviteRepository>();
builder.Services.AddScoped<IStaffEnrolmentService, StaffEnrolmentService>();
builder.Services.AddScoped<IDeviceRepository, EfDeviceRepository>();
builder.Services.AddScoped<IDeviceCredentialRepository, EfDeviceCredentialRepository>();
builder.Services.AddScoped<IDevicePairingService, DevicePairingService>();
builder.Services.AddScoped<IDeviceUnlockService, DeviceUnlockService>();
builder.Services.AddScoped<IRegisterSessionService, RegisterSessionService>();
builder.Services.AddScoped<ICustomerDisplayRepository, EfCustomerDisplayRepository>();
builder.Services.AddScoped<ICustomerDisplayService, CustomerDisplayService>();
builder.Services.AddScoped<IUserRepository, EfUserRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, EfRefreshTokenRepository>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();
builder.Services.AddScoped<ITokenRefreshService, TokenRefreshService>();

builder.Services.AddScoped<IUnitOfWork, EfUnitOfWork>();
builder.Services.AddScoped<ITenantRepository, EfTenantRepository>();
builder.Services.AddScoped<IBranchRepository, EfBranchRepository>();
builder.Services.AddScoped<IAuditLogRepository, EfAuditLogRepository>();
builder.Services.AddScoped<IDepartmentRepository, EfDepartmentRepository>();
builder.Services.AddScoped<IBootstrapTenantService, BootstrapTenantService>();
builder.Services.AddScoped<IBranchService, BranchService>();
builder.Services.AddScoped<IDepartmentService, DepartmentService>();
builder.Services.AddScoped<IDeviceManagementService, DeviceManagementService>();
builder.Services.AddScoped<ITenantSettingsService, TenantSettingsService>();
builder.Services.AddScoped<IAuditLogQueryService, AuditLogQueryService>();
builder.Services.AddScoped<IApprovalsReviewService, ApprovalsReviewService>();

builder.Services.AddScoped<ICategoryRepository, EfCategoryRepository>();
builder.Services.AddScoped<IItemRepository, EfItemRepository>();
builder.Services.AddScoped<ICatalogVersionProvider, EfCatalogVersionProvider>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IItemService, ItemService>();
builder.Services.AddScoped<IModifierGroupRepository, EfModifierGroupRepository>();
builder.Services.AddScoped<IItemModifierIngredientRepository, EfItemModifierIngredientRepository>();
builder.Services.AddScoped<ModifierDtoBuilder>();
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
builder.Services.AddScoped<IAdjustmentRepository, EfAdjustmentRepository>();
builder.Services.AddScoped<IPaymentRepository, EfPaymentRepository>();
builder.Services.AddScoped<IReceiptSequenceRepository, EfReceiptSequenceRepository>();
builder.Services.AddScoped<IKioskPrepSequenceRepository, EfKioskPrepSequenceRepository>();
builder.Services.AddSingleton<IPosSettings, PosSettings>();
builder.Services.AddScoped<IApproverAuthorizationService, ApproverAuthorizationService>();
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<IAdjustmentService, AdjustmentService>();
builder.Services.AddScoped<IKioskSessionService, KioskSessionService>();
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
builder.Services.AddScoped<IBranchScopeGuard, BranchScopeGuard>();
builder.Services.AddScoped<ISalesDashboardService, SalesDashboardService>();
builder.Services.AddScoped<IInventoryReportService, InventoryReportService>();
builder.Services.AddScoped<ITransactionExportService, TransactionExportService>();
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
builder.Services.AddScoped<IIncomingReceivingRepository, EfIncomingReceivingRepository>();
builder.Services.AddScoped<IIncomingReceivingService, IncomingReceivingService>();
builder.Services.AddScoped<IInventoryItemRepository, EfInventoryItemRepository>();
builder.Services.AddScoped<IInventoryCategoryRepository, EfInventoryCategoryRepository>();
builder.Services.AddScoped<IInventoryCategoryService, InventoryCategoryService>();
builder.Services.AddScoped<IItemStockService, ItemStockService>();
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
        // No fallback key: JwtTokenService already refuses to issue tokens without one, and a
        // well-known default here would let anyone forge tokens the API would accept.
        var jwtSigningKey = builder.Configuration["JWT_SIGNING_KEY"]
            ?? throw new InvalidOperationException("JWT_SIGNING_KEY is not configured.");
        // HS256 is only as strong as its key: a short or guessable one lets an attacker brute-force it offline
        // from any issued token and then mint admin tokens for any tenant.
        if (Encoding.UTF8.GetByteCount(jwtSigningKey) < 32)
        {
            throw new InvalidOperationException("JWT_SIGNING_KEY must be at least 32 bytes long.");
        }

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
            // Only the algorithm the tokens are actually signed with; the default 5-minute skew would let a
            // 30-minute access token (or a just-revoked session) live for 35.
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.FromSeconds(30),
            // JwtTokenService issues the role under our own claim name (JwtClaimTypes.Role),
            // not the .NET-default ClaimTypes.Role — map it here so [Authorize(Roles = "Admin")]
            // reads the right claim instead of silently never matching.
            RoleClaimType = JwtClaimTypes.Role,
        };
    });

builder.Services.AddAuthorization();

// The web admin SPA is served from a different origin than the API, so browsers block its
// requests unless that origin is listed here. Native clients aren't subject to CORS, so with
// no origins configured no policy is registered at all.
builder.Services.AddCors(options =>
{
    var allowedOrigins = (builder.Configuration["CORS_ALLOWED_ORIGINS"] ?? string.Empty)
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (allowedOrigins.Length > 0)
    {
        options.AddDefaultPolicy(policy => policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod());
    }
});

// Throttles the anonymous credential-guessing surfaces (login, kiosk pairing,
// password-reset request/confirm) — partitioned per client IP so one abusive caller
// can't exhaust another's budget. A fixed window rather than sliding/token-bucket:
// simplest option that still bounds guesses/minute, and these endpoints don't need
// smoother burst handling.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    _ = options.AddPolicy(RateLimiterPolicies.AuthSensitive, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(15),
                PermitLimit = 10,
                QueueLimit = 0,
            }));

    _ = options.AddPolicy(RateLimiterPolicies.ShiftApproval, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(15),
                PermitLimit = 10,
                QueueLimit = 0,
            }));

    _ = options.AddPolicy(RateLimiterPolicies.Refresh, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(15),
                PermitLimit = 120,
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

// One time, when the old PIN-at-a-pairing-code sign-in is retired: carries each owner over to the new sign-in, ends the old
// sessions, and sends the old devices back to waiting for a one-time code. Safe to run again. Run after "migrate".
if (args is ["migrate-legacy"])
{
    using var legacyScope = app.Services.CreateScope();
    var report = await legacyScope.ServiceProvider.GetRequiredService<LegacyMigration>().RunAsync();
    Console.WriteLine($"{report.OwnersMoved} owner(s) moved, {report.OwnersAlreadyMoved} already moved, {report.SessionsEnded} session(s) ended, {report.DevicesToPairAgain} device(s) to pair again, {report.StaffToInvite} staff member(s) to invite.");
    foreach (var link in report.OwnerLinks)
    {
        Console.WriteLine($"Owner link for {link.OwnerName} ({link.Business}), valid 30 days, shown once: /enrol/{link.Token}");
    }

    return;
}

// Local/on-prem installs have no separate CI/CD migration step — a store
// owner running the installer shouldn't need the EF Core CLI, so a
// Local-mode instance migrates its own database once at startup instead;
// Cloud's migrations run via the "migrate" preDeployCommand above.
DeploymentMode deploymentMode;

// The role check below and the migration work need separate DbContexts, so they run side by side:
// a cold start no longer waits for one database round trip after the other, and the overlap also
// builds the EF model and opens the first pooled connection once instead of twice in sequence.
var rlsCheck = Task.Run(async () =>
{
    using var rlsScope = app.Services.CreateScope();
    return await rlsScope.ServiceProvider.GetRequiredService<PurchDbContext>().Database
        .SqlQueryRaw<bool>("SELECT rolbypassrls AS \"Value\" FROM pg_roles WHERE rolname = current_user")
        .SingleAsync();
});

using (var startupScope = app.Services.CreateScope())
{
    var deploymentContext = startupScope.ServiceProvider.GetRequiredService<IDeploymentContext>();
    var dbContext = startupScope.ServiceProvider.GetRequiredService<PurchDbContext>();
    deploymentMode = deploymentContext.Mode;

    if (deploymentContext.Mode == DeploymentMode.Local)
    {
        await dbContext.Database.MigrateAsync();
    }
    else
    {
        // Cloud on Vercel has no preDeployCommand, so a deploy that adds a migration would
        // otherwise serve traffic against a stale schema (missing tables/columns -> failing
        // Items/Dashboard queries). EF takes a database-level lock while migrating, so
        // concurrent instances don't race. Deliberately non-fatal: Supabase's transaction
        // pooler can reject migration DDL, and a failed auto-migrate must not take down an
        // otherwise-working API — it is logged loudly so scripts/migrate-production.* can be run.
        var startupLogger = startupScope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
#pragma warning disable CA1031 // Intentionally broad: any migration failure must be logged, never crash startup.
        try
        {
            var pending = (await dbContext.Database.GetPendingMigrationsAsync()).ToList();
            if (pending.Count > 0)
            {
                LogApplyingMigrations(startupLogger, pending.Count, string.Join(", ", pending));
                await dbContext.Database.MigrateAsync();
                LogMigrationsApplied(startupLogger);
            }
        }
        catch (Exception exception)
        {
            LogMigrationFailed(startupLogger, exception);
        }
#pragma warning restore CA1031
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
    var bypassesRls = await rlsCheck;

    if (!bypassesRls)
    {
        throw new InvalidOperationException(
            "This backend's database connection does not bypass Row Level Security — its role lacks " +
            "BYPASSRLS/superuser. Every table has RLS enabled with no policies, so a non-bypassing connection " +
            "would silently return zero rows on every read and fail every write. Reconnect using a role with " +
            "BYPASSRLS (Supabase's postgres role, or the Postgres superuser in Local mode) before starting the app.");
    }

    // A table created by a later migration that forgot "ENABLE ROW LEVEL SECURITY" is readable through
    // Supabase's public REST API with the anon key — and since this connection bypasses RLS, nothing in
    // the app would ever notice. Report it loudly; deliberately non-fatal so it can't take the API down.
    var rlsLogger = startupScope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
#pragma warning disable CA1031 // Intentionally broad: a failed advisory check must never crash startup.
    try
    {
        var unprotected = await dbContext.Database
            .SqlQueryRaw<string>(
                "SELECT c.relname AS \"Value\" FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace " +
                "WHERE n.nspname = 'public' AND c.relkind IN ('r', 'p') AND NOT c.relrowsecurity " +
                "AND c.relname <> '__EFMigrationsHistory' ORDER BY c.relname")
            .ToListAsync();
        if (unprotected.Count > 0)
        {
            LogTablesWithoutRls(rlsLogger, string.Join(", ", unprotected));
        }
    }
    catch (Exception exception)
    {
        LogRlsAuditFailed(rlsLogger, exception);
    }
#pragma warning restore CA1031
}

// Correlation id and request logging come first so every later log line and the final status (including a 500
// produced by the exception handler) are covered. The exception handler then wraps every later
// middleware/endpoint so any thrown exception (including ones from TenantResolutionMiddleware or endpoint
// handlers) is caught and turned into a consistent ProblemDetails response, never a raw 500 with no body.
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
// CORS sits outside the exception handler on purpose: the handler clears the response it rebuilds, so with CORS inside it
// a 500 reached the browser without Access-Control-Allow-Origin and showed up as a CORS error that hid the real failure.
app.UseCors();
app.UseExceptionHandler();
app.UseResponseCompression();

// Cloud deploys (Render, Vercel) sit behind a reverse proxy, so without this every request's
// RemoteIpAddress is the proxy and the per-IP rate limiter below shares one bucket for everyone.
// Local mode is reached directly, where a client-supplied X-Forwarded-For would be spoofable.
if (deploymentMode != DeploymentMode.Local)
{
    var forwardedHeadersOptions = new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    };
    forwardedHeadersOptions.KnownNetworks.Clear();
    forwardedHeadersOptions.KnownProxies.Clear();
    _ = app.UseForwardedHeaders(forwardedHeadersOptions);
}

// Catches status codes set without a response body (e.g. JWT auth failing with a bare
// 401, [Authorize] failing with a bare 403, an unmatched route's default 404) and fills
// in a ProblemDetails body for those too, so no error response is ever silently empty.
app.UseStatusCodePages();

// Only Local mode's LocalFileStorage ever writes here (Cloud mode's
// SupabaseFileStorage never touches local disk — see IFileStorage) —
// creating these directories unconditionally used to crash Cloud-mode
// containers whose runtime image runs as a non-root user with no write
// access to its own working directory (e.g. the chiseled image used for
// Vercel's container deploy; see Dockerfile.vercel), since this ran before
// the app could serve a single request or log anything.
if (deploymentMode == DeploymentMode.Local)
{
    var webRootPath = app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");
    if (!Directory.Exists(webRootPath))
    {
        _ = Directory.CreateDirectory(webRootPath);
    }
    var defaultUploadsPath = Path.Combine(webRootPath, "uploads");
    if (!Directory.Exists(defaultUploadsPath))
    {
        _ = Directory.CreateDirectory(defaultUploadsPath);
    }

    _ = app.UseStaticFiles();
}

app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") })
    .AllowAnonymous();
app.MapAuthEndpoints();
app.MapOnboardingEndpoints();
app.MapCatalogEndpoints();
app.MapPosEndpoints();
app.MapKioskEndpoints();
app.MapDeviceDisplayEndpoints();
app.MapCustomerDisplayEndpoints();
app.MapShiftEndpoints();
app.MapPromoCodeEndpoints();
app.MapReportingEndpoints();
app.MapCreditLedgerEndpoints();
app.MapInventoryEndpoints();
app.MapSyncEndpoints();
app.MapUploadEndpoints();

app.Run();

// Exposed for WebApplicationFactory<Program> in Purch.IntegrationTests.
public partial class Program
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Applying {Count} pending migration(s): {Migrations}")]
    internal static partial void LogApplyingMigrations(ILogger logger, int count, string migrations);

    [LoggerMessage(Level = LogLevel.Information, Message = "Pending migrations applied.")]
    internal static partial void LogMigrationsApplied(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Automatic migration failed; run scripts/migrate-production against the database.")]
    internal static partial void LogMigrationFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "SECURITY: public tables without Row Level Security (exposed via Supabase REST with the anon key): {Tables}")]
    internal static partial void LogTablesWithoutRls(ILogger logger, string tables);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not audit Row Level Security coverage.")]
    internal static partial void LogRlsAuditFailed(ILogger logger, Exception exception);
}
