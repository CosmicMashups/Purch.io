using Purch.Application.Auth;

namespace Purch.UnitTests;

public sealed class PinPolicyTests
{
    [Theory]
    [InlineData("123456")]
    [InlineData("1234567")]
    [InlineData("12345678")]
    public void Six_to_eight_digits_are_accepted(string pin) => Assert.Null(PinPolicy.Validate(pin));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1234")]
    [InlineData("12345")]
    [InlineData("123456789")]
    [InlineData("12a456")]
    [InlineData("12 3456")]
    public void Anything_else_is_refused_with_a_message(string? pin) => Assert.NotNull(PinPolicy.Validate(pin));

    [Fact]
    public void The_message_states_the_six_digit_minimum() => Assert.Contains("6-8", PinPolicy.Validate("1234")!);
}
