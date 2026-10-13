namespace Purch.Application.Common;

/// <summary>
/// Commits whatever repository writes have been tracked so far in this
/// request, as one atomic transaction. A use-case that needs to create
/// several related entities (e.g. bootstrapping a tenant + branch + device +
/// admin user together) calls each repository's Add method — which only
/// stages the change — then calls SaveChangesAsync once at the end, so
/// either all of it commits or none of it does.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs a use-case that has to save more than once (a checkout stages, prices and pays in several steps) as a
    /// single database transaction: if any step throws, every save made inside it is rolled back, rather than leaving
    /// a half-finished sale behind. Retried as a whole on a transient connection failure, so the delegate must be
    /// safe to run again from the start. The default just runs it, for implementations with no transactions.
    /// </summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
        => operation(cancellationToken);
}
