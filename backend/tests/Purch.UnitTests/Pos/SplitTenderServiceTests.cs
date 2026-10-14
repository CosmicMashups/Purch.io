using Purch.Application.Common.Exceptions;
using Purch.Application.Pos;
using Purch.Domain.Enums;

namespace Purch.UnitTests.Pos;

public sealed class SplitTenderServiceTests
{
    private static readonly Guid Ledger = Guid.NewGuid();

    private static PaymentPartRequest Part(PaymentMethod method, decimal amount, decimal? tendered = null, Guid? ledger = null, string? reference = null)
        => new(method, amount, tendered, ledger, reference);

    [Fact]
    public void Cash_100_and_gcash_50_on_a_150_total_builds_two_confirmed_payments_in_order()
    {
        var result = SplitTenderService.Build(150m, [Part(PaymentMethod.Cash, 100m, 100m), Part(PaymentMethod.ManualGcashQr, 50m, reference: "GC-123")]);

        Assert.Equal(2, result.Payments.Count);
        Assert.Equal([0, 1], result.Payments.Select(p => p.Sequence));
        Assert.All(result.Payments, p => Assert.Equal(PaymentStatus.Confirmed, p.Status));
        Assert.Equal(PaymentMethod.Cash, result.Payments[0].Method);
        Assert.Equal(100m, result.Payments[0].Amount);
        Assert.Equal(0m, result.Payments[0].ChangeGiven);
        Assert.Equal(PaymentMethod.ManualGcashQr, result.Payments[1].Method);
        Assert.Equal(50m, result.Payments[1].Amount);
        Assert.Null(result.Payments[1].ChangeGiven);
        Assert.Equal("GC-123", result.Payments[1].Reference);
        Assert.Equal(0m, result.ChangeGiven);
    }

    [Theory]
    [InlineData(149.99)]
    [InlineData(150.01)]
    public void Parts_that_do_not_sum_to_the_total_are_rejected(double cash)
    {
        _ = Assert.Throws<ValidationException>(() => SplitTenderService.Build(200m, [Part(PaymentMethod.Cash, (decimal)cash), Part(PaymentMethod.BankTransfer, 50m)]));
    }

    [Fact]
    public void Cash_tendered_200_for_a_100_part_gives_100_change()
    {
        var result = SplitTenderService.Build(150m, [Part(PaymentMethod.ManualGcashQr, 50m), Part(PaymentMethod.Cash, 100m, 200m)]);

        Assert.Equal(100m, result.ChangeGiven);
        Assert.Equal(200m, result.Payments[1].AmountTendered);
        Assert.Equal(100m, result.Payments[1].ChangeGiven);
        Assert.Equal(100m, result.Payments[1].Amount);
    }

    [Fact]
    public void Cash_tendered_below_its_part_is_rejected()
    {
        _ = Assert.Throws<ValidationException>(() => SplitTenderService.Build(150m, [Part(PaymentMethod.Cash, 100m, 99m), Part(PaymentMethod.BankTransfer, 50m)]));
    }

    [Fact]
    public void An_overpaid_gcash_part_is_rejected()
    {
        _ = Assert.Throws<ValidationException>(() => SplitTenderService.Build(150m, [Part(PaymentMethod.Cash, 100m), Part(PaymentMethod.ManualGcashQr, 50m, 60m)]));
    }

    [Fact]
    public void A_non_cash_tendered_equal_to_its_amount_is_accepted()
    {
        var result = SplitTenderService.Build(150m, [Part(PaymentMethod.Cash, 100m), Part(PaymentMethod.BankTransfer, 50m, 50m)]);

        Assert.Equal(2, result.Payments.Count);
    }

    [Fact]
    public void Two_utang_parts_are_rejected()
    {
        _ = Assert.Throws<ValidationException>(() => SplitTenderService.Build(150m, [Part(PaymentMethod.UtangCredit, 100m, ledger: Ledger), Part(PaymentMethod.UtangCredit, 50m, ledger: Ledger)]));
    }

    [Fact]
    public void An_utang_part_without_a_credit_account_is_rejected()
    {
        _ = Assert.Throws<ValidationException>(() => SplitTenderService.Build(150m, [Part(PaymentMethod.Cash, 100m), Part(PaymentMethod.UtangCredit, 50m)]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void A_zero_or_negative_part_is_rejected(double amount)
    {
        _ = Assert.Throws<ValidationException>(() => SplitTenderService.Build(150m, [Part(PaymentMethod.Cash, 150m - (decimal)amount), Part(PaymentMethod.BankTransfer, (decimal)amount)]));
    }

    [Fact]
    public void A_part_with_more_than_two_decimals_is_rejected()
    {
        _ = Assert.Throws<ValidationException>(() => SplitTenderService.Build(150m, [Part(PaymentMethod.Cash, 99.995m), Part(PaymentMethod.BankTransfer, 50.005m)]));
    }

    [Theory]
    [InlineData(PaymentMethod.Split)]
    [InlineData(PaymentMethod.QrPh)]
    [InlineData(PaymentMethod.BillPaymentELoad)]
    public void Methods_not_supported_for_checkout_are_rejected(PaymentMethod method)
    {
        _ = Assert.Throws<ValidationException>(() => SplitTenderService.Build(150m, [Part(PaymentMethod.Cash, 100m), Part(method, 50m)]));
    }

    [Fact]
    public void An_empty_part_list_is_rejected()
    {
        _ = Assert.Throws<ValidationException>(() => SplitTenderService.Build(150m, []));
    }

    [Fact]
    public void A_reference_over_100_characters_is_rejected()
    {
        _ = Assert.Throws<ValidationException>(() => SplitTenderService.Build(150m, [Part(PaymentMethod.Cash, 100m), Part(PaymentMethod.BankTransfer, 50m, reference: new string('x', 101))]));
    }

    [Fact]
    public void A_single_part_for_the_full_total_is_accepted()
    {
        var result = SplitTenderService.Build(150m, [Part(PaymentMethod.Cash, 150m, 200m)]);

        var payment = Assert.Single(result.Payments);
        Assert.Equal(0, payment.Sequence);
        Assert.Equal(50m, result.ChangeGiven);
    }
}
