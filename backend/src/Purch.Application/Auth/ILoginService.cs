namespace Purch.Application.Auth;

public interface ILoginService
{
    Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
}
