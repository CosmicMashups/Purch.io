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

    public async Task<BranchDto> UpdateManualGcashQrSettingsAsync(
        Guid branchId,
        UpdateManualGcashQrSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        var branch = await branchRepository.GetByIdAsync(branchId, cancellationToken)
            ?? throw new NotFoundException("Branch", branchId);

        // A QR image with no account name/number to double-check against is a
        // support headache waiting to happen — the cashier has nothing to read
        // back to the customer if the scan doesn't look right.
        if (!string.IsNullOrWhiteSpace(request.QrImageUrl) &&
            string.IsNullOrWhiteSpace(request.AccountName) &&
            string.IsNullOrWhiteSpace(request.AccountNumber))
        {
            throw new ValidationException(
                nameof(request.AccountName),
                "Add the GCash account name or number so staff can verify the QR matches.");
        }

        branch.ManualGcashQrImageUrl = request.QrImageUrl?.Trim();
        branch.ManualGcashAccountName = request.AccountName?.Trim();
        branch.ManualGcashAccountNumber = request.AccountNumber?.Trim();

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
        branch.CashDrawerPolicy,
        branch.ManualGcashQrImageUrl,
        branch.ManualGcashAccountName,
        branch.ManualGcashAccountNumber);
    }
}
