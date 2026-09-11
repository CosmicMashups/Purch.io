/// Mirrors Purch.Application.Catalog.BundlePromoRuleDto.
class BundlePromoRule {
  const BundlePromoRule({
    required this.id,
    required this.description,
    required this.triggerQuantity,
    required this.bundlePrice,
    required this.isActive,
  });

  factory BundlePromoRule.fromJson(Map<String, dynamic> json) {
    return BundlePromoRule(
      id: json['id'] as String,
      description: json['description'] as String,
      triggerQuantity: json['triggerQuantity'] as int,
      bundlePrice: (json['bundlePrice'] as num).toDouble(),
      isActive: json['isActive'] as bool,
    );
  }

  final String id;
  final String description;
  final int triggerQuantity;
  final double bundlePrice;
  final bool isActive;
}

/// Mirrors Purch.Application.Catalog.CreateBundlePromoRuleRequest.
class CreateBundlePromoRuleRequest {
  const CreateBundlePromoRuleRequest({
    required this.description,
    required this.triggerQuantity,
    required this.bundlePrice,
  });

  final String description;
  final int triggerQuantity;
  final double bundlePrice;

  Map<String, dynamic> toJson() => {
    'description': description,
    'triggerQuantity': triggerQuantity,
    'bundlePrice': bundlePrice,
  };
}
