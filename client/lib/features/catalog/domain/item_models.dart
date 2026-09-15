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
    required this.serviceDurationMinutes,
    required this.departmentId,
    required this.lowStockThreshold,
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
      serviceDurationMinutes: json['serviceDurationMinutes'] as int?,
      departmentId: json['departmentId'] as String?,
      lowStockThreshold: (json['lowStockThreshold'] as num?)?.toDouble(),
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
  final int? serviceDurationMinutes;
  final String? departmentId;
  final double? lowStockThreshold;
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

/// Mirrors Purch.Application.Catalog.UpdateItemRequest.
class UpdateItemRequest {
  const UpdateItemRequest({
    required this.name,
    this.sku,
    this.barcode,
    this.categoryId,
    required this.basePrice,
    this.imageUrl,
    required this.isActive,
    this.departmentId,
  });

  final String name;
  final String? sku;
  final String? barcode;
  final String? categoryId;
  final double basePrice;
  final String? imageUrl;
  final bool isActive;
  final String? departmentId;

  Map<String, dynamic> toJson() => {
    'name': name,
    'sku': sku,
    'barcode': barcode,
    'categoryId': categoryId,
    'basePrice': basePrice,
    'imageUrl': imageUrl,
    'isActive': isActive,
    'departmentId': departmentId,
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

/// Mirrors Purch.Application.Catalog.UpdateServiceDurationRequest. Only
/// meaningful for service-priced items — the backend rejects it otherwise.
class UpdateServiceDurationRequest {
  const UpdateServiceDurationRequest({required this.durationMinutes});

  final int durationMinutes;

  Map<String, dynamic> toJson() => {'durationMinutes': durationMinutes};
}

/// Mirrors Purch.Application.Catalog.UpdateItemDepartmentRequest — B6's
/// department/concessionaire assignment. A null departmentId clears the
/// assignment; an item doesn't have to belong to a department.
class UpdateItemDepartmentRequest {
  const UpdateItemDepartmentRequest({this.departmentId});

  final String? departmentId;

  Map<String, dynamic> toJson() => {'departmentId': departmentId};
}

/// Mirrors Purch.Application.Catalog.UpdateLowStockThresholdRequest — C1's
/// low-stock alert threshold. A null threshold clears the alert.
class UpdateLowStockThresholdRequest {
  const UpdateLowStockThresholdRequest({this.threshold});

  final double? threshold;

  Map<String, dynamic> toJson() => {'threshold': threshold};
}
