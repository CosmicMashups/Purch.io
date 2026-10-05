import 'package:purch_client/features/inventory/domain/purchase_order_models.dart';
import 'package:purch_client/features/inventory/domain/purchase_order_repository.dart';

class FakePurchaseOrderRepository implements PurchaseOrderRepository {
  FakePurchaseOrderRepository({
    this.createPurchaseOrderFailure,
    this.markSentFailure,
    this.cancelFailure,
    List<PurchaseOrder>? initialPurchaseOrders,
  }) : purchaseOrders = initialPurchaseOrders ?? [];

  final Object? createPurchaseOrderFailure;
  final Object? markSentFailure;
  final Object? cancelFailure;
  final List<PurchaseOrder> purchaseOrders;

  CreatePurchaseOrderRequest? lastCreateRequest;

  @override
  Future<List<PurchaseOrder>> listPurchaseOrders() async => purchaseOrders;

  @override
  Future<PurchaseOrder> createPurchaseOrder(
    CreatePurchaseOrderRequest request,
  ) async {
    lastCreateRequest = request;
    if (createPurchaseOrderFailure != null) {
      throw createPurchaseOrderFailure!;
    }
    final created = PurchaseOrder(
      id: 'po-${purchaseOrders.length + 1}',
      supplierId: request.supplierId,
      supplierName: 'Supplier ${request.supplierId}',
      branchId: request.branchId,
      branchName: 'Branch ${request.branchId}',
      status: PurchaseOrderStatus.draft,
      sentAt: null,
      lines: [
        for (final line in request.lines)
          PurchaseOrderLine(
            id: 'line-${line.itemId}',
            itemId: line.itemId,
            itemName: 'Item ${line.itemId}',
            quantityOrdered: line.quantityOrdered,
            quantityReceived: 0,
            expectedUnitCost: line.expectedUnitCost,
          ),
      ],
    );
    purchaseOrders.add(created);
    return created;
  }

  @override
  Future<PurchaseOrder> markSent(String purchaseOrderId) async {
    if (markSentFailure != null) {
      throw markSentFailure!;
    }
    return _updateStatus(purchaseOrderId, PurchaseOrderStatus.sent);
  }

  @override
  Future<PurchaseOrder> cancel(String purchaseOrderId) async {
    if (cancelFailure != null) {
      throw cancelFailure!;
    }
    return _updateStatus(purchaseOrderId, PurchaseOrderStatus.cancelled);
  }

  PurchaseOrder _updateStatus(String id, PurchaseOrderStatus status) {
    final index = purchaseOrders.indexWhere((po) => po.id == id);
    final current = purchaseOrders[index];
    final updated = PurchaseOrder(
      id: current.id,
      supplierId: current.supplierId,
      supplierName: current.supplierName,
      branchId: current.branchId,
      branchName: current.branchName,
      status: status,
      sentAt:
          status == PurchaseOrderStatus.sent
              ? DateTime(2026, 1, 1)
              : current.sentAt,
      lines: current.lines,
    );
    purchaseOrders[index] = updated;
    return updated;
  }
}
