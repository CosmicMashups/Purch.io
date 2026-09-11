using Purch.Application.Auth;

namespace Purch.Infrastructure.Auth;

public sealed class BCryptPinHasher : IPinHasher
{
    public string Hash(string pin)
    {
        return BCrypt.Net.BCrypt.HashPassword(pin);
    }

    public bool Verify(string pin, string hash)
    {
        return BCrypt.Net.BCrypt.Verify(pin, hash);
    }
}
