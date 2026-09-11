/// Mirrors Purch.Application.Catalog.ItemVariantDto.
class ItemVariant {
  const ItemVariant({
    required this.id,
    required this.attributes,
    required this.sku,
    required this.stockOnHand,
    required this.priceOverride,
    required this.imageUrl,
  });

  factory ItemVariant.fromJson(Map<String, dynamic> json) {
    return ItemVariant(
      id: json['id'] as String,
      attributes:
          (json['attributes'] as Map<String, dynamic>).cast<String, String>(),
      sku: json['sku'] as String?,
      stockOnHand: (json['stockOnHand'] as num).toDouble(),
      priceOverride: (json['priceOverride'] as num?)?.toDouble(),
      imageUrl: json['imageUrl'] as String?,
    );
  }

  final String id;
  final Map<String, String> attributes;
  final String? sku;
  final double stockOnHand;
  final double? priceOverride;
  final String? imageUrl;

  String get attributesLabel =>
      attributes.entries.map((e) => '${e.key}: ${e.value}').join(', ');
}

/// Mirrors Purch.Application.Catalog.CreateItemVariantRequest.
class CreateItemVariantRequest {
  const CreateItemVariantRequest({
    required this.attributes,
    this.sku,
    this.priceOverride,
    this.imageUrl,
  });

  final Map<String, String> attributes;
  final String? sku;
  final double? priceOverride;
  final String? imageUrl;

  Map<String, dynamic> toJson() => {
    'attributes': attributes,
    'sku': sku,
    'priceOverride': priceOverride,
    'imageUrl': imageUrl,
  };
}
