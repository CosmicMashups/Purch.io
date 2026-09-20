using Purch.Application.Auth;
using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.Application.Onboarding;

public sealed class BootstrapTenantService(
    ITenantRepository tenantRepository,
    IBranchRepository branchRepository,
    IDeviceRepository deviceRepository,
    IUserRepository userRepository,
    IPinHasher pinHasher,
    IPasswordHasher passwordHasher,
    IDeploymentContext deploymentContext,
    IUnitOfWork unitOfWork) : IBootstrapTenantService
{
    public async Task<BootstrapTenantResult> BootstrapAsync(BootstrapTenantRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request);

        var tenant = new Tenant
        {
            Name = request.TenantName.Trim(),
            BusinessType = request.BusinessType,
            // Stamped from the running instance's own config, never from caller
            // input — a Cloud-mode API must never create a tenant that claims
            // to be Local, or vice versa. See docs/adr on deployment mode.
            DeploymentMode = deploymentContext.Mode,
        };
        tenantRepository.Add(tenant);

        var branch = new Branch { TenantId = tenant.Id, Name = request.BranchName.Trim() };
        branchRepository.Add(branch);

        var device = new Device
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            PairingCode = PairingCodeGenerator.Generate(),
        };
        deviceRepository.Add(device);

        var admin = new User
        {
            TenantId = tenant.Id,
            Name = request.AdminName.Trim(),
            Role = Role.Admin,
            ScopeType = ScopeType.Tenant,
            PinHash = pinHasher.Hash(request.AdminPin),
            Email = request.AdminEmail?.Trim(),
            PasswordHash = request.AdminPassword is { Length: > 0 }
                ? passwordHasher.Hash(request.AdminPassword)
                : null,
            IsActive = true,
        };
        userRepository.Add(admin);

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return new BootstrapTenantResult(tenant.Id, branch.Id, device.Id, device.PairingCode, admin.Id);
    }

    private static void Validate(BootstrapTenantRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.TenantName))
        {
            errors[nameof(request.TenantName)] = ["Business name is required."];
        }

        if (string.IsNullOrWhiteSpace(request.BranchName))
        {
            errors[nameof(request.BranchName)] = ["Branch name is required."];
        }

        if (string.IsNullOrWhiteSpace(request.AdminName))
        {
            errors[nameof(request.AdminName)] = ["Admin name is required."];
        }

        if (PinPolicy.Validate(request.AdminPin) is { } adminPinError)
        {
            errors[nameof(request.AdminPin)] = [adminPinError.Replace("PIN", "Admin PIN", StringComparison.Ordinal)];
        }

        var hasEmail = !string.IsNullOrWhiteSpace(request.AdminEmail);
        var hasPassword = !string.IsNullOrWhiteSpace(request.AdminPassword);
        if (hasPassword && PasswordPolicy.Validate(request.AdminPassword) is { } adminPasswordError)
        {
            errors[nameof(request.AdminPassword)] = [adminPasswordError];
        }

        if (hasEmail != hasPassword)
        {
            errors[nameof(request.AdminEmail)] =
                ["Admin email and password must both be provided together, or both omitted."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
    }
}
