using Purch.Domain.Entities;

namespace Purch.Infrastructure.Auth;

public interface IJwtTokenService
{
    string IssueAccessToken(User user);
}
