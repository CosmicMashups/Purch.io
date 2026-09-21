namespace Purch.Application.Common;

/// <summary>Bounds for the append-only log endpoints (audit log, stock movements). They page newest-first:
/// pass the last row's CreatedAt as <c>before</c> to fetch the next page. The default keeps unpaged
/// callers working while capping how much any one request can pull.</summary>
public static class Paging
{
    public const int DefaultLimit = 200;
    public const int MaxLimit = 500;

    public static int ClampLimit(int? requested)
    {
        return requested is not { } limit ? DefaultLimit : Math.Clamp(limit, 1, MaxLimit);
    }
}
