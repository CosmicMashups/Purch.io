using Purch.Domain.Enums;

namespace Purch.Application.Inventory.Import;

public interface IImportService
{
    /// <summary>Parses the file and stores a Previewed batch. The same Idempotency-Key (same tenant) returns the existing batch.</summary>
    Task<ImportBatchDto> UploadAsync(Stream content, ImportKind kind, string? idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>The batch with its plan rebuilt from the stored rows, the saved resolutions and the current catalog.</summary>
    Task<ImportBatchDto> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Replaces the saved resolutions (decision key to answer) and returns the re-planned batch.</summary>
    Task<ImportBatchDto> SaveResolutionsAsync(Guid id, IReadOnlyDictionary<string, ImportResolutionDto> resolutions, CancellationToken cancellationToken = default);

    /// <summary>Re-plans on the server (before the PIN and again inside the transaction) and applies the batch all or
    /// nothing. A stale preview returns <see cref="ImportCommitResultDto.Committed"/> false with the fresh plan.</summary>
    Task<ImportCommitResultDto> CommitAsync(Guid id, CommitImportRequest request, CancellationToken cancellationToken = default);
}
