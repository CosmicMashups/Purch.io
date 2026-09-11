namespace Purch.Application.Auth;

public sealed class LoginService(
    IDeviceRepository deviceRepository,
    IUserRepository userRepository,
    IPinHasher pinHasher,
    IJwtTokenService jwtTokenService) : ILoginService
{
    public async Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
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

        var accessToken = jwtTokenService.IssueAccessToken(matchedUser);
        return new LoginResult.Success(accessToken);
    }
}
