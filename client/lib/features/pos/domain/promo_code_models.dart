import '../../../core/formatting/money.dart';

/// Mirrors Purch.Domain.Enums.PromoDiscountType exactly, in declared order.
/// fixedPrice was appended for the automatic item-discount promo rules (see
/// item_promo_models.dart) — index-based serialization, so it must stay last.
enum PromoDiscountType { percentage, fixedAmount, fixedPrice }

/// Mirrors Purch.Application.Promotions.PromoCodeDto — a cart-level code the
/// cashier types in at checkout, distinct from the catalog feature's
/// BundlePromoRule (an item-specific "buy N get bundle price" rule with no
/// code entry).
class PromoCode {
  const PromoCode({
    required this.id,
    required this.code,
    required this.discountType,
    required this.discountValue,
    required this.isActive,
    required this.expiresAt,
  });

  factory PromoCode.fromJson(Map<String, dynamic> json) {
    return PromoCode(
      id: json['id'] as String,
      code: json['code'] as String,
      discountType: PromoDiscountType.values[json['discountType'] as int],
      discountValue: (json['discountValue'] as num).toDouble(),
      isActive: json['isActive'] as bool,
      expiresAt:
          json['expiresAt'] == null
              ? null
              : DateTime.parse(json['expiresAt'] as String),
    );
  }

  final String id;
  final String code;
  final PromoDiscountType discountType;
  final double discountValue;
  final bool isActive;
  final DateTime? expiresAt;

  String get discountLabel =>
      discountType == PromoDiscountType.percentage
          ? '${discountValue.toStringAsFixed(0)}% off'
          : '${formatCurrency(discountValue)} off';
}

/// Mirrors Purch.Application.Promotions.CreatePromoCodeRequest.
class CreatePromoCodeRequest {
  const CreatePromoCodeRequest({
    required this.code,
    required this.discountType,
    required this.discountValue,
    this.expiresAt,
  });

  final String code;
  final PromoDiscountType discountType;
  final double discountValue;
  final DateTime? expiresAt;

  Map<String, dynamic> toJson() => {
    'code': code,
    'discountType': discountType.index,
    'discountValue': discountValue,
    'expiresAt': expiresAt?.toIso8601String(),
  };
}
