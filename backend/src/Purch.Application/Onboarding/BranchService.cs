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

        var existing = await branchRepository.ListByTenantAsync(CurrentTenantId, cancellationToken);
        var code = string.IsNullOrWhiteSpace(request.Code)
            ? DefaultCode(request.Name, existing)
            : NormalizeCode(request.Code, existing, null);

        var branch = new Branch
        {
            TenantId = CurrentTenantId,
            Name = request.Name.Trim(),
            Address = request.Address?.Trim(),
            Code = code,
        };

        branchRepository.Add(branch);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(branch);
    }

    public async Task<BranchDto> UpdateDetailsAsync(Guid branchId, UpdateBranchDetailsRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationException(nameof(request.Name), "Branch name is required.");
        }

        var branch = await branchRepository.GetByIdAsync(branchId, cancellationToken)
            ?? throw new NotFoundException("Branch", branchId);
        var all = await branchRepository.ListByTenantAsync(CurrentTenantId, cancellationToken);

        branch.Name = request.Name.Trim();
        branch.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
        branch.Code = NormalizeCode(request.Code, all, branch.Id);

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(branch);
    }

    /// <summary>First letters of the name, numbered when another branch already uses it.</summary>
    internal static string DefaultCode(string name, IReadOnlyList<Branch> existing)
    {
        var letters = new string(name.Where(char.IsLetterOrDigit).Take(4).ToArray()).ToUpperInvariant();
        var baseCode = letters.Length == 0 ? "BR" : letters;
        var code = baseCode;
        for (var n = 2; existing.Any(b => string.Equals(b.Code, code, StringComparison.OrdinalIgnoreCase)); n++)
        {
            code = baseCode + n;
        }
        return code;
    }

    private static string NormalizeCode(string value, IReadOnlyList<Branch> existing, Guid? selfId)
    {
        var code = value.Trim().ToUpperInvariant();
        if (code.Length is < 2 or > 12 || !code.All(char.IsAsciiLetterOrDigit))
        {
            throw new ValidationException("Code", "Branch code must be 2 to 12 letters or numbers.");
        }

        if (existing.Any(b => b.Id != selfId && string.Equals(b.Code, code, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ValidationException("Code", "Another branch already uses this code.");
        }

        return code;
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
        branch.ManualGcashAccountNumber,
        branch.IsActive,
        branch.Code);
    }
}
