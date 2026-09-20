namespace Purch.Application.Inventory;

public interface IBranchTransferService
{
    Task<IReadOnlyList<BranchTransferDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<BranchTransferDto> CreateAsync(CreateBranchTransferRequest request, CancellationToken cancellationToken = default);

    /// <summary>Pending -> InTransit. Deducts every line's quantity from the source branch's stock.</summary>
    Task<BranchTransferDto> MarkInTransitAsync(Guid branchTransferId, CancellationToken cancellationToken = default);

    /// <summary>InTransit -> Received. Adds every line's quantity to the destination branch's stock.</summary>
    Task<BranchTransferDto> MarkReceivedAsync(Guid branchTransferId, CancellationToken cancellationToken = default);

    /// <summary>Calls off a transfer that hasn't arrived. A Pending one had not touched stock, so nothing
    /// changes; an InTransit one had already left the source's count, so that stock is put back.</summary>
    Task<BranchTransferDto> CancelAsync(Guid branchTransferId, CancellationToken cancellationToken = default);
}
