import 'package:purch_client/features/inventory/domain/branch_transfer_models.dart';
import 'package:purch_client/features/inventory/domain/branch_transfer_repository.dart';

class FakeBranchTransferRepository implements BranchTransferRepository {
  FakeBranchTransferRepository({
    this.createTransferFailure,
    this.markInTransitFailure,
    this.markReceivedFailure,
    List<BranchTransfer>? initialTransfers,
  }) : transfers = initialTransfers ?? [];

  final Object? createTransferFailure;
  final Object? markInTransitFailure;
  final Object? markReceivedFailure;
  final List<BranchTransfer> transfers;

  CreateBranchTransferRequest? lastCreateRequest;

  @override
  Future<List<BranchTransfer>> listTransfers() async => transfers;

  @override
  Future<BranchTransfer> createTransfer(
    CreateBranchTransferRequest request,
  ) async {
    lastCreateRequest = request;
    if (createTransferFailure != null) {
      throw createTransferFailure!;
    }
    final created = BranchTransfer(
      id: 'transfer-${transfers.length + 1}',
      sourceBranchId: request.sourceBranchId,
      sourceBranchName: 'Branch ${request.sourceBranchId}',
      destinationBranchId: request.destinationBranchId,
      destinationBranchName: 'Branch ${request.destinationBranchId}',
      status: BranchTransferStatus.pending,
      lines: [
        for (final line in request.lines)
          BranchTransferLine(
            id: 'line-${line.itemId}',
            itemId: line.itemId,
            itemName: 'Item ${line.itemId}',
            quantity: line.quantity,
          ),
      ],
    );
    transfers.add(created);
    return created;
  }

  @override
  Future<BranchTransfer> markInTransit(String branchTransferId) async {
    if (markInTransitFailure != null) {
      throw markInTransitFailure!;
    }
    return _updateStatus(branchTransferId, BranchTransferStatus.inTransit);
  }

  @override
  Future<BranchTransfer> markReceived(String branchTransferId) async {
    if (markReceivedFailure != null) {
      throw markReceivedFailure!;
    }
    return _updateStatus(branchTransferId, BranchTransferStatus.received);
  }

  @override
  Future<BranchTransfer> cancel(String branchTransferId) async {
    return _updateStatus(branchTransferId, BranchTransferStatus.cancelled);
  }

  BranchTransfer _updateStatus(String id, BranchTransferStatus status) {
    final index = transfers.indexWhere((transfer) => transfer.id == id);
    final current = transfers[index];
    final updated = BranchTransfer(
      id: current.id,
      sourceBranchId: current.sourceBranchId,
      sourceBranchName: current.sourceBranchName,
      destinationBranchId: current.destinationBranchId,
      destinationBranchName: current.destinationBranchName,
      status: status,
      lines: current.lines,
    );
    transfers[index] = updated;
    return updated;
  }
}
