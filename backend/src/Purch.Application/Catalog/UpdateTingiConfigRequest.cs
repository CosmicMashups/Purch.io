using Purch.Domain.Enums;

namespace Purch.Application.Catalog;

public sealed record UpdateTingiConfigRequest(
    TingiMode TingiMode,
    decimal? PackagedSize,
    decimal? TingiIncrementStep,
    IReadOnlyList<decimal>? AllowedSizes);
