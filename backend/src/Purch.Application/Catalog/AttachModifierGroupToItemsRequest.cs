namespace Purch.Application.Catalog;

/// <summary>Attaches one modifier group to many items at once: every item of <paramref name="CategoryId"/>, or the
/// <paramref name="ItemIds"/> chosen one by one. Give one of the two. Items that already have the group are left alone.</summary>
public sealed record AttachModifierGroupToItemsRequest(Guid? CategoryId = null, IReadOnlyList<Guid>? ItemIds = null);

/// <summary>How many items got the group now, and how many already had it.</summary>
public sealed record AttachModifierGroupToItemsResult(int Attached, int AlreadyAttached);
