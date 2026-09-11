import 'purchase_order_models.dart';

/// C5 — creating a PO against a supplier, sending it, and receiving stock
/// against it (possibly across several partial deliveries).
abstract class PurchaseOrderRepository {
  Future<List<PurchaseOrder>> listPurchaseOrders();

  Future<PurchaseOrder> createPurchaseOrder(CreatePurchaseOrderRequest request);

  Future<PurchaseOrder> markSent(String purchaseOrderId);

  Future<PurchaseOrder> cancel(String purchaseOrderId);

  Future<PurchaseOrder> receive(
    String purchaseOrderId,
    ReceivePurchaseOrderRequest request,
  );
}
