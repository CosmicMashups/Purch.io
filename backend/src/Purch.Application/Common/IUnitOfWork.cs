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
}
