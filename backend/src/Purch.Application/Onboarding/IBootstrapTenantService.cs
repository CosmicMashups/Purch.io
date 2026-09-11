namespace Purch.Application.Onboarding;

public interface IBootstrapTenantService
{
    Task<BootstrapTenantResult> BootstrapAsync(BootstrapTenantRequest request, CancellationToken cancellationToken = default);
}
