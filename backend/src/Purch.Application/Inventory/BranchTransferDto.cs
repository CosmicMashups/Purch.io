using Purch.Domain.Enums;

namespace Purch.Application.Inventory;

public sealed record BranchTransferDto(
    Guid Id,
    Guid SourceBranchId,
    string SourceBranchName,
    Guid DestinationBranchId,
    string DestinationBranchName,
    BranchTransferStatus Status,
    IReadOnlyList<BranchTransferLineDto> Lines);

public sealed record BranchTransferLineDto(
    Guid Id,
    Guid ItemId,
    string ItemName,
    decimal Quantity);

public sealed record CreateBranchTransferRequest(
    Guid SourceBranchId,
    Guid DestinationBranchId,
    IReadOnlyList<CreateBranchTransferLineRequest> Lines);

public sealed record CreateBranchTransferLineRequest(Guid ItemId, decimal Quantity);
