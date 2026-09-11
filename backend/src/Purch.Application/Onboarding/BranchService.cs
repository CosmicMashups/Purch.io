using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.Application.Onboarding;

public sealed class BranchService(
    IBranchRepository branchRepository,
    ICurrentTenantProvider currentTenantProvider,
    IUnitOfWork unitOfWork) : IBranchService
{
    public async Task<IReadOnlyList<BranchDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var branches = await branchRepository.ListByTenantAsync(CurrentTenantId, cancellationToken);
        return [.. branches.Select(ToDto)];
    }

    public async Task<BranchDto> CreateAsync(CreateBranchRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationException(nameof(request.Name), "Branch name is required.");
        }

        var branch = new Branch
        {
            TenantId = CurrentTenantId,
            Name = request.Name.Trim(),
            Address = request.Address?.Trim(),
        };

        branchRepository.Add(branch);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(branch);
    }

    public async Task<BranchDto> UpdateHardwareSettingsAsync(
        Guid branchId,
        UpdateBranchHardwareSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        var branch = await branchRepository.GetByIdAsync(branchId, cancellationToken)
            ?? throw new NotFoundException("Branch", branchId);

        // A cash drawer has no connection of its own — it's triggered through the
        // receipt printer's kick signal, so enabling one without a printer profile
        // is a meaningless configuration, not just an unusual one.
        if (request.CashDrawerEnabled && request.ReceiptPrinterProfile == ReceiptPrinterProfile.None)
        {
            throw new ValidationException(
                nameof(request.CashDrawerEnabled),
                "A cash drawer requires a receipt printer profile, since the drawer is triggered through the printer.");
        }

        branch.ReceiptPrinterProfile = request.ReceiptPrinterProfile;
        branch.CashDrawerEnabled = request.CashDrawerEnabled;
        branch.CashDrawerPolicy = request.CashDrawerPolicy;

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(branch);
    }

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("Branch management requires an authenticated tenant context.");

    private static BranchDto ToDto(Branch branch)
    {
        return new(
        branch.Id,
        branch.Name,
        branch.Address,
        branch.ReceiptPrinterProfile,
        branch.CashDrawerEnabled,
        branch.CashDrawerPolicy);
    }
}
