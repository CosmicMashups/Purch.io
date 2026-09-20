using Purch.Application.Common.Exceptions;
using Purch.Application.Reporting;

namespace Purch.Application.Common;

/// <summary>Stops a branch- or department-scoped account from changing another branch's data. Reports
/// already narrow what such an account may READ to its own branch; stock movements, transfers, purchase
/// orders and stock counts take a branch id straight from the request, so without this a branch manager
/// could act on any branch of the tenant. It asks the same resolver the reports use, so there is one
/// definition of which branch an account is confined to (tenant-scoped accounts and unattended devices
/// are not confined).</summary>
public interface IBranchScopeGuard
{
    /// <summary>Throws ForbiddenException unless the caller may act on <paramref name="branchId"/>.</summary>
    Task EnsureAllowedAsync(Guid branchId, CancellationToken cancellationToken = default);

    /// <summary>Throws ForbiddenException unless the caller may act on at least one of the branches (e.g. either
    /// end of a transfer).</summary>
    Task EnsureAnyAllowedAsync(IReadOnlyCollection<Guid> branchIds, CancellationToken cancellationToken = default);
}

public sealed class BranchScopeGuard(IReportScopeResolver scopeResolver) : IBranchScopeGuard
{
    public Task EnsureAllowedAsync(Guid branchId, CancellationToken cancellationToken = default)
    {
        return EnsureAnyAllowedAsync([branchId], cancellationToken);
    }

    public async Task EnsureAnyAllowedAsync(IReadOnlyCollection<Guid> branchIds, CancellationToken cancellationToken = default)
    {
        var confinedTo = await scopeResolver.ResolveBranchIdAsync(null, cancellationToken);
        if (confinedTo is { } branchId && !branchIds.Contains(branchId))
        {
            throw new ForbiddenException("Your account is limited to a different branch.");
        }
    }
}
