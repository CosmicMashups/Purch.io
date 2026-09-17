namespace Purch.Application.Auth;

public interface ILoginService
{
    Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>Separate login path for tenant admins/owners (e.g. back-office use),
    /// independent of the device+PIN flow above.</summary>
    Task<LoginResult> AdminLoginAsync(AdminLoginRequest request, CancellationToken cancellationToken = default);
}
