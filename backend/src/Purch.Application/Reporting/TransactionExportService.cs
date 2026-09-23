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

        var tenantId = currentTenantProvider.TenantId
            ?? throw new InvalidOperationException("The transaction export requires an authenticated tenant context.");
        var branchNamesById = (await branchRepository.ListByTenantAsync(tenantId, cancellationToken))
            .ToDictionary(branch => branch.Id, branch => branch.Name);
        var staffNamesById = (await userRepository.ListByTenantAsync(tenantId, cancellationToken))
            .ToDictionary(user => user.Id, user => user.Name);

        var csv = new StringBuilder();
        _ = csv.AppendLine("Receipt Number,Date/Time,Branch,Staff,Total,Discount,Senior/PWD Discount Applied,Promo Code,Order Type");

        foreach (var transaction in transactions.OrderBy(t => t.CreatedAt))
        {
            _ = csv.AppendLine(string.Join(
                ',',
                transaction.ReceiptNumber?.ToString(CultureInfo.InvariantCulture) ?? "",
                transaction.CreatedAt.UtcDateTime.ToString("o", CultureInfo.InvariantCulture),
                CsvField(branchNamesById.GetValueOrDefault(transaction.BranchId, "(deleted branch)")),
                CsvField(transaction.StaffUserId is { } staffId ? staffNamesById.GetValueOrDefault(staffId, "(removed user)") : ""),
                transaction.TotalAmount.ToString(CultureInfo.InvariantCulture),
                transaction.DiscountAmount.ToString(CultureInfo.InvariantCulture),
                transaction.SeniorPwdDiscountApplied ? "Yes" : "No",
                CsvField(transaction.PromoCode ?? ""),
                CsvField(transaction.OrderType ?? "")));
        }

        return csv.ToString();
    }

    private static string CsvField(string value)
    {
        // A cell that starts with one of these is run as a formula when the file is opened in a
        // spreadsheet, and both branch/staff names and the promo code are user-entered.
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
        {
            value = "'" + value;
        }

        return value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }
}
