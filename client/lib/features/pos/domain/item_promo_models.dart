/// Mirrors Purch.Application.Promotions.{Bogo,Combo,ItemDiscount}PromoRuleDto —
/// automatic (no code), item-targeted, time-boxed promos that apply
/// themselves at checkout. Distinct from PromoCode (a cart-level code the
/// cashier types in) in promo_code_models.dart.
library;

import 'promo_code_models.dart';

/// Mirrors Purch.Application.Promotions.BogoPromoRuleDto.
class BogoPromoRule {
  const BogoPromoRule({
    required this.id,
    required this.name,
    required this.triggerItemId,
    required this.triggerQuantity,
    required this.freeItemId,
    required this.freeQuantity,
    required this.startsAt,
    required this.endsAt,
    required this.isActive,
  });

  factory BogoPromoRule.fromJson(Map<String, dynamic> json) {
    return BogoPromoRule(
      id: json['id'] as String,
      name: json['name'] as String,
      triggerItemId: json['triggerItemId'] as String,
      triggerQuantity: (json['triggerQuantity'] as num).toInt(),
      freeItemId: json['freeItemId'] as String,
      freeQuantity: (json['freeQuantity'] as num).toInt(),
      startsAt:
          json['startsAt'] == null
              ? null
              : DateTime.parse(json['startsAt'] as String),
      endsAt:
          json['endsAt'] == null
              ? null
              : DateTime.parse(json['endsAt'] as String),
      isActive: json['isActive'] as bool,
    );
  }

  final String id;
  final String name;
  final String triggerItemId;
  final int triggerQuantity;
  final String freeItemId;
  final int freeQuantity;
  final DateTime? startsAt;
  final DateTime? endsAt;
  final bool isActive;
}

/// Mirrors Purch.Application.Promotions.CreateBogoPromoRuleRequest.
class CreateBogoPromoRuleRequest {
  const CreateBogoPromoRuleRequest({
    required this.name,
    required this.triggerItemId,
    required this.triggerQuantity,
    required this.freeItemId,
    required this.freeQuantity,
    this.startsAt,
    this.endsAt,
  });

  final String name;
  final String triggerItemId;
  final int triggerQuantity;
  final String freeItemId;
  final int freeQuantity;
  final DateTime? startsAt;
  final DateTime? endsAt;

  Map<String, dynamic> toJson() => {
    'name': name,
    'triggerItemId': triggerItemId,
    'triggerQuantity': triggerQuantity,
    'freeItemId': freeItemId,
    'freeQuantity': freeQuantity,
    'startsAt': startsAt?.toIso8601String(),
    'endsAt': endsAt?.toIso8601String(),
  };
}

/// Mirrors Purch.Application.Promotions.UpdateBogoPromoRuleRequest.
class UpdateBogoPromoRuleRequest {
  const UpdateBogoPromoRuleRequest({
    required this.name,
    required this.triggerItemId,
    required this.triggerQuantity,
    required this.freeItemId,
    required this.freeQuantity,
    this.startsAt,
    this.endsAt,
    required this.isActive,
  });

  final String name;
  final String triggerItemId;
  final int triggerQuantity;
  final String freeItemId;
  final int freeQuantity;
  final DateTime? startsAt;
  final DateTime? endsAt;
  final bool isActive;

  Map<String, dynamic> toJson() => {
    'name': name,
    'triggerItemId': triggerItemId,
    'triggerQuantity': triggerQuantity,
    'freeItemId': freeItemId,
    'freeQuantity': freeQuantity,
    'startsAt': startsAt?.toIso8601String(),
    'endsAt': endsAt?.toIso8601String(),
    'isActive': isActive,
  };
}

/// Mirrors Purch.Application.Promotions.ComboPromoRuleDto.
class ComboPromoRule {
  const ComboPromoRule({
    required this.id,
    required this.name,
    required this.itemAId,
    required this.itemBId,
    required this.comboPrice,
    required this.startsAt,
    required this.endsAt,
    required this.isActive,
  });

  factory ComboPromoRule.fromJson(Map<String, dynamic> json) {
    return ComboPromoRule(
      id: json['id'] as String,
      name: json['name'] as String,
      itemAId: json['itemAId'] as String,
      itemBId: json['itemBId'] as String,
      comboPrice: (json['comboPrice'] as num).toDouble(),
      startsAt:
          json['startsAt'] == null
              ? null
              : DateTime.parse(json['startsAt'] as String),
      endsAt:
          json['endsAt'] == null
              ? null
              : DateTime.parse(json['endsAt'] as String),
      isActive: json['isActive'] as bool,
    );
  }

  final String id;
  final String name;
  final String itemAId;
  final String itemBId;
  final double comboPrice;
  final DateTime? startsAt;
  final DateTime? endsAt;
  final bool isActive;
}

/// Mirrors Purch.Application.Promotions.CreateComboPromoRuleRequest.
class CreateComboPromoRuleRequest {
  const CreateComboPromoRuleRequest({
    required this.name,
    required this.itemAId,
    required this.itemBId,
    required this.comboPrice,
    this.startsAt,
    this.endsAt,
  });

  final String name;
  final String itemAId;
  final String itemBId;
  final double comboPrice;
  final DateTime? startsAt;
  final DateTime? endsAt;

  Map<String, dynamic> toJson() => {
    'name': name,
    'itemAId': itemAId,
    'itemBId': itemBId,
    'comboPrice': comboPrice,
    'startsAt': startsAt?.toIso8601String(),
    'endsAt': endsAt?.toIso8601String(),
  };
}

/// Mirrors Purch.Application.Promotions.UpdateComboPromoRuleRequest.
class UpdateComboPromoRuleRequest {
  const UpdateComboPromoRuleRequest({
    required this.name,
    required this.itemAId,
    required this.itemBId,
    required this.comboPrice,
    this.startsAt,
    this.endsAt,
    required this.isActive,
  });

  final String name;
  final String itemAId;
  final String itemBId;
  final double comboPrice;
  final DateTime? startsAt;
  final DateTime? endsAt;
  final bool isActive;

  Map<String, dynamic> toJson() => {
    'name': name,
    'itemAId': itemAId,
    'itemBId': itemBId,
    'comboPrice': comboPrice,
    'startsAt': startsAt?.toIso8601String(),
    'endsAt': endsAt?.toIso8601String(),
    'isActive': isActive,
  };
}

/// Mirrors Purch.Application.Promotions.ItemDiscountPromoRuleDto.
class ItemDiscountPromoRule {
  const ItemDiscountPromoRule({
    required this.id,
    required this.name,
    required this.itemId,
    required this.discountType,
    required this.discountValue,
    required this.startsAt,
    required this.endsAt,
    required this.isActive,
  });

  factory ItemDiscountPromoRule.fromJson(Map<String, dynamic> json) {
    return ItemDiscountPromoRule(
      id: json['id'] as String,
      name: json['name'] as String,
      itemId: json['itemId'] as String,
      discountType: PromoDiscountType.values[json['discountType'] as int],
      discountValue: (json['discountValue'] as num).toDouble(),
      startsAt:
          json['startsAt'] == null
              ? null
              : DateTime.parse(json['startsAt'] as String),
      endsAt:
          json['endsAt'] == null
              ? null
              : DateTime.parse(json['endsAt'] as String),
      isActive: json['isActive'] as bool,
    );
  }

  final String id;
  final String name;
  final String itemId;
  final PromoDiscountType discountType;
  final double discountValue;
  final DateTime? startsAt;
  final DateTime? endsAt;
  final bool isActive;

  String get discountLabel {
    switch (discountType) {
      case PromoDiscountType.percentage:
        return '${discountValue.toStringAsFixed(0)}% off';
      case PromoDiscountType.fixedAmount:
        return '₱${discountValue.toStringAsFixed(2)} off';
      case PromoDiscountType.fixedPrice:
        return 'Fixed ₱${discountValue.toStringAsFixed(2)}';
    }
  }
}

/// Mirrors Purch.Application.Promotions.CreateItemDiscountPromoRuleRequest.
class CreateItemDiscountPromoRuleRequest {
  const CreateItemDiscountPromoRuleRequest({
    required this.name,
    required this.itemId,
    required this.discountType,
    required this.discountValue,
    this.startsAt,
    this.endsAt,
  });

  final String name;
  final String itemId;
  final PromoDiscountType discountType;
  final double discountValue;
  final DateTime? startsAt;
  final DateTime? endsAt;

  Map<String, dynamic> toJson() => {
    'name': name,
    'itemId': itemId,
    'discountType': discountType.index,
    'discountValue': discountValue,
    'startsAt': startsAt?.toIso8601String(),
    'endsAt': endsAt?.toIso8601String(),
  };
}

/// Mirrors Purch.Application.Promotions.UpdateItemDiscountPromoRuleRequest.
class UpdateItemDiscountPromoRuleRequest {
  const UpdateItemDiscountPromoRuleRequest({
    required this.name,
    required this.itemId,
    required this.discountType,
    required this.discountValue,
    this.startsAt,
    this.endsAt,
    required this.isActive,
  });

  final String name;
  final String itemId;
  final PromoDiscountType discountType;
  final double discountValue;
  final DateTime? startsAt;
  final DateTime? endsAt;
  final bool isActive;

  Map<String, dynamic> toJson() => {
    'name': name,
    'itemId': itemId,
    'discountType': discountType.index,
    'discountValue': discountValue,
    'startsAt': startsAt?.toIso8601String(),
    'endsAt': endsAt?.toIso8601String(),
    'isActive': isActive,
  };
}
