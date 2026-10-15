using System.Globalization;
using System.Text;
using Purch.Application.Auth;
using Purch.Application.Common;
using Purch.Application.Onboarding;

namespace Purch.Application.Reporting;

public sealed class TransactionExportService(
    IReportingRepository reportingRepository,
    IBranchRepository branchRepository,
    IUserRepository userRepository,
    ICurrentTenantProvider currentTenantProvider,
    IReportScopeResolver reportScopeResolver) : ITransactionExportService
{
    public async Task<string> GenerateTransactionsCsvAsync(
        Guid? branchId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default)
    {
        var resolvedBranchId = await reportScopeResolver.ResolveBranchIdAsync(branchId, cancellationToken);
        var transactions = await reportingRepository.ListCompletedTransactionsAsync(resolvedBranchId, fromUtc, toUtc, cancellationToken);
        if (transactions.Count > IReportingRepository.MaxExportRows)
        {
            throw new Common.Exceptions.ValidationException(
                "range",
                $"That period has more than {IReportingRepository.MaxExportRows:N0} sales. Choose a shorter date range and export it in parts.");
        }

        var tenantId = currentTenantProvider.TenantId
            ?? throw new InvalidOperationException("The transaction export requires an authenticated tenant context.");
        var branchNamesById = (await branchRepository.ListByTenantAsync(tenantId, cancellationToken))
            .ToDictionary(branch => branch.Id, branch => branch.Name);
        var staffNamesById = (await userRepository.ListActorsAsync(tenantId, cancellationToken))
            .ToDictionary(user => user.Id, user => user.Name);

        var csv = new StringBuilder();
        _ = csv.AppendLine("Receipt Number,Date/Time,Branch,Staff,Total,Discount,Senior/PWD Discount Applied,Promo Code,Order Type");

        foreach (var transaction in transactions.OrderBy(t => t.CompletedAt ?? t.CreatedAt))
        {
            _ = csv.AppendLine(string.Join(
                ',',
                transaction.ReceiptNumber?.ToString(CultureInfo.InvariantCulture) ?? "",
                (transaction.CompletedAt ?? transaction.CreatedAt).UtcDateTime.ToString("o", CultureInfo.InvariantCulture),
                ReportCsv.Field(branchNamesById.GetValueOrDefault(transaction.BranchId, "(deleted branch)")),
                ReportCsv.Field(transaction.StaffUserId is { } staffId ? staffNamesById.GetValueOrDefault(staffId, "(removed user)") : ""),
                transaction.TotalAmount.ToString(CultureInfo.InvariantCulture),
                transaction.DiscountAmount.ToString(CultureInfo.InvariantCulture),
                transaction.SeniorPwdDiscountApplied ? "Yes" : "No",
                ReportCsv.Field(transaction.PromoCode ?? ""),
                ReportCsv.Field(transaction.OrderType ?? "")));
        }

        return csv.ToString();
    }
}
