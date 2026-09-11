import 'branch_transfer_models.dart';

/// C4 — moving stock between two branches of the same tenant.
abstract class BranchTransferRepository {
  Future<List<BranchTransfer>> listTransfers();

  Future<BranchTransfer> createTransfer(CreateBranchTransferRequest request);

  Future<BranchTransfer> markInTransit(String branchTransferId);

  Future<BranchTransfer> markReceived(String branchTransferId);
}
