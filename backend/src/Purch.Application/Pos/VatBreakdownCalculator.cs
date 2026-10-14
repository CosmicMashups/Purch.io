namespace Purch.Application.Pos;

/// <summary>How a completed sale's amount splits for VAT reporting.</summary>
public sealed record VatBreakdown(decimal VatableSales, decimal VatAmount, decimal VatExemptSales, decimal ZeroRatedSales, decimal Rate);

/// <summary>Pure VAT split of a VAT-inclusive amount. VatableSales + VatAmount always equals the net payable exactly.</summary>
public static class VatBreakdownCalculator
{
    public const decimal DefaultRate = 0.12m;

    public static VatBreakdown Compute(decimal netPayable, decimal vatExemptAmount, decimal rate = DefaultRate)
    {
        var vatable = Math.Round(netPayable / (1 + rate), 2, MidpointRounding.AwayFromZero);
        return new VatBreakdown(vatable, netPayable - vatable, vatExemptAmount, 0m, rate);
    }
}
