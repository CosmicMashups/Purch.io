using Purch.Application.Auth;
using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.Application.Onboarding;

public sealed class BootstrapTenantService(
    ITenantRepository tenantRepository,
    IBranchRepository branchRepository,
    IPinHasher pinHasher,
    IDeploymentContext deploymentContext,
    IAccountService accountService,
    IAccountRepository accountRepository,
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

        // The owner is an account (email and password, checked by the identity provider) with an Admin membership here. If
        // the email already has an account, the password must be that account's: anyone may register a business, but only the
        // person who knows the password may attach it to an existing login. No device is registered at this point: the
        // owner's session is an ordinary personal-device sign-in, and devices are added afterwards from the Devices page.
        var account = await accountService.CreateAccountAsync(request.AdminEmail!, request.AdminName, request.AdminPassword!, cancellationToken);
        var membership = new Membership
        {
            TenantId = tenant.Id,
            AccountId = account.Id,
            Role = MembershipRole.Admin,
            PinHash = pinHasher.Hash(request.AdminPin),
        };
        accountRepository.Add(membership);

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return new BootstrapTenantResult(tenant.Id, branch.Id, membership.Id);
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

        if (string.IsNullOrWhiteSpace(request.AdminEmail))
        {
            errors[nameof(request.AdminEmail)] = ["Admin email is required."];
        }

        if (PasswordPolicy.Validate(request.AdminPassword) is { } adminPasswordError)
        {
            errors[nameof(request.AdminPassword)] = [adminPasswordError];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
    }
}
