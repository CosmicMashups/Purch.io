import 'purchase_order_models.dart';

/// C5 — creating a PO against a supplier and submitting it. Deliveries are
/// recorded through Incoming Receiving Reports.
abstract class PurchaseOrderRepository {
  Future<List<PurchaseOrder>> listPurchaseOrders();

  Future<PurchaseOrder> createPurchaseOrder(CreatePurchaseOrderRequest request);

  Future<PurchaseOrder> markSent(String purchaseOrderId);

  Future<PurchaseOrder> cancel(String purchaseOrderId);
}
