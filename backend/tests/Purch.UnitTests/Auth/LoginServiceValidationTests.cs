using Purch.Application.Auth;
using Purch.Application.Common.Exceptions;
using Purch.Domain.Entities;

namespace Purch.UnitTests.Auth;

public sealed class LoginServiceValidationTests
{
    [Theory]
    [InlineData("", "1234")]
    [InlineData("   ", "1234")]
    [InlineData("DEVICE-1", "")]
    [InlineData("DEVICE-1", "   ")]
    public async Task Empty_or_whitespace_input_throws_a_validation_exception_before_touching_the_database(string pairingCode, string pin)
    {
        var loginService = new LoginService(
            new NeverCalledDeviceRepository(),
            new NeverCalledUserRepository(),
            new NeverCalledPinHasher(),
            new NeverCalledJwtTokenService());

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => loginService.LoginAsync(new LoginRequest(pairingCode, pin)));

        Assert.True(exception.Errors.ContainsKey("DevicePairingCode") || exception.Errors.ContainsKey("Pin"));
    }

    // These fakes throw if ever called — proving validation short-circuits before any
    // repository/hasher/token work happens, not just that an exception is eventually thrown.
    private sealed class NeverCalledDeviceRepository : IDeviceRepository
    {
        public Task<Device?> FindByPairingCodeAsync(string pairingCode, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Should not be called when validation fails.");
        }
    }

    private sealed class NeverCalledUserRepository : IUserRepository
    {
        public Task<IReadOnlyList<User>> GetActiveUsersByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Should not be called when validation fails.");
        }
    }

    private sealed class NeverCalledPinHasher : IPinHasher
    {
        public string Hash(string pin)
        {
            throw new InvalidOperationException("Should not be called when validation fails.");
        }

        public bool Verify(string pin, string hash)
        {
            throw new InvalidOperationException("Should not be called when validation fails.");
        }
    }

    private sealed class NeverCalledJwtTokenService : IJwtTokenService
    {
        public string IssueAccessToken(User user)
        {
            throw new InvalidOperationException("Should not be called when validation fails.");
        }
    }
}
