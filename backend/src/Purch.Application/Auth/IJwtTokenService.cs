using Purch.Domain.Entities;

namespace Purch.Application.Auth;

public interface IJwtTokenService
{
    string IssueAccessToken(User user);
}
