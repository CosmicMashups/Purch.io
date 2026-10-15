using Purch.Domain.Entities;

namespace Purch.Application.Inventory;

/// <summary>Control numbers and purchase order money, shared by requests and orders.</summary>
internal static class PurchaseDocuments
{
    public const string RequestKind = "PR";
    public const string OrderKind = "PO";

    public static async Task<string> NextControlNumberAsync(
        IPurchaseRequestRepository repository, Guid tenantId, Branch branch, string kind, CancellationToken cancellationToken)
    {
        var sequence = await repository.GetOrCreateSequenceAsync(tenantId, branch.Id, kind, cancellationToken);
        sequence.LastNumber++;
        var code = string.IsNullOrWhiteSpace(branch.Code) ? "BR" : branch.Code.Trim().ToUpperInvariant();
        return $"{code}-{kind}-{sequence.LastNumber:D5}";
    }

    public static string? DeliveryAddress(Branch branch, Tenant? tenant, int branchCount)
    {
        var registered = tenant?.RegisteredAddress;
        var own = branch.Address;
        return branchCount <= 1
            ? (string.IsNullOrWhiteSpace(registered) ? own : registered)
            : (string.IsNullOrWhiteSpace(own) ? null : own);
    }

    /// <summary>Exclusive: VAT is added on top of (subtotal - discount). Inclusive: VAT is the share of it already in the price.</summary>
    public static (decimal Subtotal, decimal Discount, decimal Vat, decimal Total) Totals(
        IEnumerable<(decimal Quantity, decimal UnitPrice)> lines, decimal discount, bool vatInclusive, decimal vatRate)
    {
        var subtotal = Math.Round(lines.Sum(line => line.Quantity * line.UnitPrice), 2);
        var appliedDiscount = Math.Round(Math.Clamp(discount, 0m, subtotal), 2);
        var net = subtotal - appliedDiscount;
        if (vatInclusive)
        {
            return (subtotal, appliedDiscount, Math.Round(net - (net / (1 + vatRate)), 2), net);
        }

        var vat = Math.Round(net * vatRate, 2);
        return (subtotal, appliedDiscount, vat, net + vat);
    }
}
