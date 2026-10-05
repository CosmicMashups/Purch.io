import 'incoming_receiving_models.dart';

/// Incoming Receiving Reports: what arrived, who took it in, and in what
/// condition. A report may be recorded before its purchase order exists.
abstract class IncomingReceivingRepository {
  Future<List<IncomingReceiving>> listReports();

  Future<IncomingReceiving> createReport(CreateIncomingReceivingRequest request);

  Future<IncomingReceiving> linkPurchaseOrder(
    String reportId,
    String purchaseOrderId,
  );
}
