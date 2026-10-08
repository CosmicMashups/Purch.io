namespace Purch.Domain.Common;

/// <summary>
/// A record that is never physically removed. "Deleted" hides it from lists and from selling while keeping every
/// sale, receipt and audit row that points at it intact, and an Admin or Manager can restore it. Deliberately not
/// a global query filter: a filter would also hide the row from history joins (a receipt line whose item was
/// deleted), so list, search and sell queries exclude deleted rows explicitly.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }

    DateTimeOffset? DeletedAt { get; set; }

    Guid? DeletedByUserId { get; set; }
}
