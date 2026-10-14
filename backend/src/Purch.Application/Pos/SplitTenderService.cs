using Purch.Application.Common.Exceptions;
using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.Application.Pos;

/// <summary>The validated tenders of one sale: unsaved <see cref="Payment"/> rows (caller sets TenantId, TransactionId,
/// CreatedAt) in Sequence order, and the total change handed back (cash parts only).</summary>
public sealed record SplitTenderResult(IReadOnlyList<Payment> Payments, decimal ChangeGiven);

/// <summary>Pure validation and construction of a split payment. Nothing is persisted here, so a rejected split
/// leaves no partial Payment rows. Rules: 1..MaxParts parts; each Amount positive with at most 2 decimals; amounts sum
/// to the total exactly; methods limited to <see cref="SupportedMethods"/>; only Cash may be tendered above its Amount
/// (change = tendered - amount); a non-cash AmountTendered is null or equal to Amount; at most one UtangCredit part and
/// it needs a CustomerCreditLedgerId; Reference at most 100 characters. The credit limit itself is checked by the
/// caller against the Utang part's amount.</summary>
public static class SplitTenderService
{
    public const int MaxParts = 10;

    public const int MaxReferenceLength = 100;

    /// <summary>Methods a sale can be paid with today. QrPh, BillPaymentELoad and the Split marker are not.</summary>
    public static readonly IReadOnlySet<PaymentMethod> SupportedMethods = new HashSet<PaymentMethod>
    {
        PaymentMethod.Cash,
        PaymentMethod.BankTransfer,
        PaymentMethod.ManualGcashQr,
        PaymentMethod.UtangCredit,
    };

    private const string Field = nameof(RecordPaymentRequest.Parts);

    public static SplitTenderResult Build(decimal total, IReadOnlyList<PaymentPartRequest> parts)
    {
        if (parts is null || parts.Count == 0)
        {
            throw new ValidationException(Field, "Add at least one payment.");
        }

        if (parts.Count > MaxParts)
        {
            throw new ValidationException(Field, $"A sale can be split into at most {MaxParts} payments.");
        }

        var payments = new List<Payment>(parts.Count);
        var change = 0m;
        for (var index = 0; index < parts.Count; index++)
        {
            var part = parts[index] ?? throw new ValidationException(Field, "A payment part is missing.");
            if (!SupportedMethods.Contains(part.Method))
            {
                throw new ValidationException(Field, $"{part.Method} isn't available for checkout yet — only Cash, Bank Transfer, Manual GCash QR, and Utang/Credit are supported so far.");
            }

            if (part.Amount <= 0 || decimal.Round(part.Amount, 2) != part.Amount)
            {
                throw new ValidationException(Field, "Each payment amount must be more than zero, in pesos and centavos.");
            }

            decimal? partChange = null;
            if (part.Method == PaymentMethod.Cash)
            {
                var tendered = part.AmountTendered ?? part.Amount;
                if (tendered < part.Amount || decimal.Round(tendered, 2) != tendered)
                {
                    throw new ValidationException(Field, "Cash tendered must cover its part of the payment.");
                }

                partChange = tendered - part.Amount;
                change += partChange.Value;
            }
            else if (part.AmountTendered is { } tendered && tendered != part.Amount)
            {
                throw new ValidationException(Field, "Only a cash payment can be more than its amount; enter the exact amount for the other payments.");
            }

            if (part.Method == PaymentMethod.UtangCredit && part.CustomerCreditLedgerId is null)
            {
                throw new ValidationException(Field, "A customer credit account is required for Utang/Credit payments.");
            }

            var reference = string.IsNullOrWhiteSpace(part.Reference) ? null : part.Reference.Trim();
            if (reference is { Length: > MaxReferenceLength })
            {
                throw new ValidationException(Field, $"A payment reference can be at most {MaxReferenceLength} characters.");
            }

            payments.Add(new Payment
            {
                Method = part.Method,
                Status = PaymentStatus.Confirmed,
                Amount = part.Amount,
                AmountTendered = part.Method == PaymentMethod.Cash ? part.AmountTendered ?? part.Amount : part.AmountTendered,
                ChangeGiven = partChange,
                Sequence = index,
                Reference = reference,
            });
        }

        if (parts.Count(p => p.Method == PaymentMethod.UtangCredit) > 1)
        {
            throw new ValidationException(Field, "Only one Utang/Credit payment is allowed per sale.");
        }

        if (payments.Sum(p => p.Amount) != total)
        {
            throw new ValidationException(Field, $"The payments must add up to the total of {total:0.00}.");
        }

        return new SplitTenderResult(payments, change);
    }
}
