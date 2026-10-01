import '../../pos/domain/transaction_models.dart';

/// Mirrors Purch.Application.Pos.PlaceKioskOrderRequest — the kiosk's whole
/// order, built entirely on-device, in one call. [orderId] is the idempotency
/// key: resending the same id after a lost response returns the order that
/// already went through instead of placing it twice.
class PlaceKioskOrderRequest {
  const PlaceKioskOrderRequest({
    required this.orderId,
    required this.lines,
    required this.orderType,
  });

  final String orderId;
  final List<AddTransactionLineRequest> lines;
  final String orderType;

  Map<String, dynamic> toJson() => {
    'orderId': orderId,
    'lines': lines.map((l) => l.toJson()).toList(),
    'orderType': orderType,
  };
}
