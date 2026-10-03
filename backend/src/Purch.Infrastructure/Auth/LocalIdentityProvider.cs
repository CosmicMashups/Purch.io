using Microsoft.EntityFrameworkCore;
using Purch.Application.Auth;
using Purch.Application.Common.Exceptions;
using Purch.Domain.Entities;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Auth;

/// <summary>Password check against our own database, for Local deployment mode and tests. Cloud mode uses
/// <see cref="SupabaseIdentityProvider"/>. Emails here are already lower-cased by the caller.</summary>
public sealed class LocalIdentityProvider(PurchDbContext dbContext, IPasswordHasher passwordHasher) : IIdentityProvider
{
    // Checked for an unknown email too, so the time taken does not reveal whether the account exists.
    private static readonly string DummyHash = BCrypt.Net.BCrypt.HashPassword("not-a-real-password");

    public async Task<Guid> CreateUserAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        if (await dbContext.LocalCredentials.AnyAsync(c => c.Email == email, cancellationToken))
        {
            throw new ConflictException("That email already has a login.");
        }

        var credential = new LocalCredential { Email = email, PasswordHash = passwordHasher.Hash(password) };
        _ = dbContext.LocalCredentials.Add(credential);
        _ = await dbContext.SaveChangesAsync(cancellationToken);
        return credential.Id;
    }

    public async Task<Guid?> VerifyPasswordAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var credential = await dbContext.LocalCredentials.AsNoTracking().FirstOrDefaultAsync(c => c.Email == email, cancellationToken);
        if (credential is null)
        {
            _ = passwordHasher.Verify(password, DummyHash);
            return null;
        }

        return passwordHasher.Verify(password, credential.PasswordHash) ? credential.Id : null;
    }

    public async Task SetPasswordAsync(Guid providerUserId, string newPassword, CancellationToken cancellationToken = default)
    {
        var credential = await dbContext.LocalCredentials.FirstOrDefaultAsync(c => c.Id == providerUserId, cancellationToken)
            ?? throw new NotFoundException("Login", providerUserId);
        credential.PasswordHash = passwordHasher.Hash(newPassword);
        _ = await dbContext.SaveChangesAsync(cancellationToken);
    }
}
