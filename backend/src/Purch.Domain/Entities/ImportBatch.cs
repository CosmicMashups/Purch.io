using Purch.Domain.Common;
using Purch.Domain.Enums;

namespace Purch.Domain.Entities;

/// <summary>An uploaded inventory CSV awaiting review/commit. Parsed rows, plan, resolutions and result are stored
/// as jsonb; the batch expires 24h after upload. Unique per (TenantId, IdempotencyKey).</summary>
public class ImportBatch : TenantScopedEntity
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    public ImportKind Kind { get; set; }

    public ImportBatchStatus Status { get; set; } = ImportBatchStatus.Previewed;

    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>SHA-256 hex of the uploaded bytes.</summary>
    public string ContentHash { get; set; } = string.Empty;

    public string ParsedJson { get; set; } = "{}";

    public string PlanJson { get; set; } = "{}";

    public string ResolutionsJson { get; set; } = "{}";

    public string? ResultJson { get; set; }

    public Guid CreatedBy { get; set; }

    public Guid? CommittedBy { get; set; }

    public Guid? ApprovedByUserId { get; set; }

    public DateTimeOffset? CommittedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }
}
