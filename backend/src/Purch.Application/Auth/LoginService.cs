using Purch.Application.Common.Exceptions;

namespace Purch.Application.Auth;

public sealed class LoginService(
    IDeviceRepository deviceRepository,
    IUserRepository userRepository,
    IPinHasher pinHasher,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService) : ILoginService
{
    public async Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request);

        var device = await deviceRepository.FindByPairingCodeAsync(request.DevicePairingCode, cancellationToken);
        if (device is null)
        {
            return new LoginResult.InvalidDevice();
        }

        var activeUsers = await userRepository.GetActiveUsersByTenantAsync(device.TenantId, cancellationToken);

        var matchedUser = activeUsers.FirstOrDefault(user => pinHasher.Verify(request.Pin, user.PinHash));
        if (matchedUser is null)
        {
            return new LoginResult.InvalidPin();
        }

        var accessToken = jwtTokenService.IssueAccessToken(matchedUser, device);
        return new LoginResult.Success(accessToken);
    }

    public async Task<LoginResult> AdminLoginAsync(AdminLoginRequest request, CancellationToken cancellationToken = default)
    {
        ValidateAdmin(request);

        var user = await userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (user is null || !user.IsActive || user.PasswordHash is null ||
            !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            return new LoginResult.InvalidAdminCredentials();
        }

        var accessToken = jwtTokenService.IssueAdminAccessToken(user);
        return new LoginResult.Success(accessToken);
    }

    private static void ValidateAdmin(AdminLoginRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            errors[nameof(request.Email)] = ["Email is required."];
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            errors[nameof(request.Password)] = ["Password is required."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
    }

    private static void Validate(LoginRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.DevicePairingCode))
        {
            errors[nameof(request.DevicePairingCode)] = ["Device pairing code is required."];
        }

        if (string.IsNullOrWhiteSpace(request.Pin))
        {
            errors[nameof(request.Pin)] = ["PIN is required."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
    }
}
