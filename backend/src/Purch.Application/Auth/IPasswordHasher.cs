namespace Purch.Application.Auth;

/// <summary>Separate from <see cref="IPinHasher"/> even though both are BCrypt today —
/// PIN and password have different length/complexity rules and could diverge later.</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string hash);
}
