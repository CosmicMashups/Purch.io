import 'package:purch_client/features/inventory/domain/purchase_order_models.dart';
import 'package:purch_client/features/inventory/domain/purchase_order_repository.dart';

class FakePurchaseOrderRepository implements PurchaseOrderRepository {
  FakePurchaseOrderRepository({
    this.createPurchaseOrderFailure,
    this.markSentFailure,
    this.cancelFailure,
    this.receiveFailure,
    List<PurchaseOrder>? initialPurchaseOrders,
  }) : purchaseOrders = initialPurchaseOrders ?? [];

  final Object? createPurchaseOrderFailure;
  final Object? markSentFailure;
  final Object? cancelFailure;
  final Object? receiveFailure;
  final List<PurchaseOrder> purchaseOrders;

  CreatePurchaseOrderRequest? lastCreateRequest;
  ReceivePurchaseOrderRequest? lastReceiveRequest;

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

  @override
  Future<PurchaseOrder> receive(
    String purchaseOrderId,
    ReceivePurchaseOrderRequest request,
  ) async {
    lastReceiveRequest = request;
    if (receiveFailure != null) {
      throw receiveFailure!;
    }
    final index = purchaseOrders.indexWhere((po) => po.id == purchaseOrderId);
    final current = purchaseOrders[index];
    final updatedLines = [
      for (final line in current.lines)
        if (request.lines.any((r) => r.lineId == line.id))
          PurchaseOrderLine(
            id: line.id,
            itemId: line.itemId,
            itemName: line.itemName,
            quantityOrdered: line.quantityOrdered,
            quantityReceived:
                line.quantityReceived +
                request.lines
                    .firstWhere((r) => r.lineId == line.id)
                    .receivedQuantity,
            expectedUnitCost: line.expectedUnitCost,
          )
        else
          line,
    ];
    final allReceived = updatedLines.every(
      (line) => line.quantityReceived >= line.quantityOrdered,
    );
    final updated = PurchaseOrder(
      id: current.id,
      supplierId: current.supplierId,
      supplierName: current.supplierName,
      branchId: current.branchId,
      branchName: current.branchName,
      status:
          allReceived
              ? PurchaseOrderStatus.received
              : PurchaseOrderStatus.partiallyReceived,
      sentAt: current.sentAt,
      lines: updatedLines,
    );
    purchaseOrders[index] = updated;
    return updated;
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
