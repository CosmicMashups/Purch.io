using Purch.Application.Pos;

namespace Purch.UnitTests.Pos;

public sealed class VatBreakdownCalculatorTests
{
    [Fact]
    public void A_vat_inclusive_amount_splits_into_vatable_sales_and_vat()
    {
        var result = VatBreakdownCalculator.Compute(112m, 0m);
        Assert.Equal(100.00m, result.VatableSales);
        Assert.Equal(12.00m, result.VatAmount);
        Assert.Equal(0m, result.VatExemptSales);
        Assert.Equal(0m, result.ZeroRatedSales);
        Assert.Equal(0.12m, result.Rate);
    }

    [Fact]
    public void A_zero_sale_is_all_zero()
    {
        var result = VatBreakdownCalculator.Compute(0m, 0m);
        Assert.Equal(0m, result.VatableSales);
        Assert.Equal(0m, result.VatAmount);
        Assert.Equal(0m, result.VatExemptSales);
        Assert.Equal(0m, result.ZeroRatedSales);
    }

    [Fact]
    public void A_senior_pwd_sale_keeps_the_exempt_amount_and_splits_exactly()
    {
        var result = VatBreakdownCalculator.Compute(160m, 24m);
        Assert.Equal(24m, result.VatExemptSales);
        Assert.Equal(160m, result.VatableSales + result.VatAmount);
    }

    [Theory]
    [InlineData(0.01)]
    [InlineData(33.33)]
    [InlineData(99.99)]
    [InlineData(1234.57)]
    public void Rounding_never_loses_a_centavo(double amount)
    {
        var net = (decimal)amount;
        var result = VatBreakdownCalculator.Compute(net, 0m);
        Assert.Equal(net, result.VatableSales + result.VatAmount);
        Assert.True(Math.Abs(result.VatableSales - net / 1.12m) <= 0.01m);
    }
}
