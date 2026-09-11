namespace Purch.Application.Inventory;

public interface IBranchTransferService
{
    Task<IReadOnlyList<BranchTransferDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<BranchTransferDto> CreateAsync(CreateBranchTransferRequest request, CancellationToken cancellationToken = default);

    /// <summary>Pending -> InTransit. Deducts every line's quantity from the source branch's stock.</summary>
    Task<BranchTransferDto> MarkInTransitAsync(Guid branchTransferId, CancellationToken cancellationToken = default);

    /// <summary>InTransit -> Received. Adds every line's quantity to the destination branch's stock.</summary>
    Task<BranchTransferDto> MarkReceivedAsync(Guid branchTransferId, CancellationToken cancellationToken = default);
}
