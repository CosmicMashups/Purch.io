using Purch.Domain.Entities;

namespace Purch.Application.Pos;

/// <summary>
/// The manager/admin PIN check behind a void, refund or exchange (see TransactionService). Always requires a
/// PIN, always requires it to belong to a DIFFERENT active Admin/Manager account than whoever is signed in
/// and asking, and locks the terminal out for a while after too many wrong PINs — so a cashier who has
/// watched a manager type their PIN cannot simply guess it, and every use is traceable to the manager or
/// admin who actually gave it, never to whoever happened to be signed in.
/// </summary>
public interface IApproverAuthorizationService
{
    /// <summary>Returns the approver on success. Throws <see cref="Purch.Application.Common.Exceptions.ValidationException"/>
    /// for a missing/wrong PIN or a self-approval attempt, and <see cref="Purch.Application.Common.Exceptions.ForbiddenException"/>
    /// while the terminal is locked out.</summary>
    Task<User> AuthorizeAsync(string? pin, CancellationToken cancellationToken = default);
}
