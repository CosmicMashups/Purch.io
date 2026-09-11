import 'pricing_type.dart';
import 'tingi_mode.dart';

/// Mirrors Purch.Application.Catalog.ItemDto.
class Item {
  const Item({
    required this.id,
    required this.name,
    required this.sku,
    required this.barcode,
    required this.categoryId,
    required this.basePrice,
    required this.imageUrl,
    required this.pricingType,
    required this.stockOnHand,
    required this.isActive,
    required this.tingiMode,
    required this.packagedSize,
    required this.tingiIncrementStep,
    required this.tingiAllowedSizes,
  });

  factory Item.fromJson(Map<String, dynamic> json) {
    return Item(
      id: json['id'] as String,
      name: json['name'] as String,
      sku: json['sku'] as String?,
      barcode: json['barcode'] as String?,
      categoryId: json['categoryId'] as String?,
      basePrice: (json['basePrice'] as num).toDouble(),
      imageUrl: json['imageUrl'] as String?,
      pricingType: PricingType.values[json['pricingType'] as int],
      stockOnHand: (json['stockOnHand'] as num).toDouble(),
      isActive: json['isActive'] as bool,
      tingiMode: TingiMode.values[json['tingiMode'] as int],
      packagedSize: (json['packagedSize'] as num?)?.toDouble(),
      tingiIncrementStep: (json['tingiIncrementStep'] as num?)?.toDouble(),
      tingiAllowedSizes:
          (json['tingiAllowedSizes'] as List<dynamic>)
              .map((size) => (size as num).toDouble())
              .toList(),
    );
  }

  final String id;
  final String name;
  final String? sku;
  final String? barcode;
  final String? categoryId;
  final double basePrice;
  final String? imageUrl;
  final PricingType pricingType;
  final double stockOnHand;
  final bool isActive;
  final TingiMode tingiMode;
  final double? packagedSize;
  final double? tingiIncrementStep;
  final List<double> tingiAllowedSizes;
}

/// Mirrors Purch.Application.Catalog.CreateItemRequest. PricingType is fixed
/// at creation and never changed afterward — see the backend's own note.
class CreateItemRequest {
  const CreateItemRequest({
    required this.name,
    this.sku,
    this.barcode,
    this.categoryId,
    required this.basePrice,
    this.imageUrl,
    required this.pricingType,
  });

  final String name;
  final String? sku;
  final String? barcode;
  final String? categoryId;
  final double basePrice;
  final String? imageUrl;
  final PricingType pricingType;

  Map<String, dynamic> toJson() => {
    'name': name,
    'sku': sku,
    'barcode': barcode,
    'categoryId': categoryId,
    'basePrice': basePrice,
    'imageUrl': imageUrl,
    'pricingType': pricingType.index,
  };
}

/// Mirrors Purch.Application.Catalog.UpdateTingiConfigRequest. Only meaningful
/// for weight/volume items — the backend rejects it otherwise.
class UpdateTingiConfigRequest {
  const UpdateTingiConfigRequest({
    required this.tingiMode,
    this.packagedSize,
    this.tingiIncrementStep,
    this.allowedSizes,
  });

  final TingiMode tingiMode;
  final double? packagedSize;
  final double? tingiIncrementStep;
  final List<double>? allowedSizes;

  Map<String, dynamic> toJson() => {
    'tingiMode': tingiMode.index,
    'packagedSize': packagedSize,
    'tingiIncrementStep': tingiIncrementStep,
    'allowedSizes': allowedSizes,
  };
}
