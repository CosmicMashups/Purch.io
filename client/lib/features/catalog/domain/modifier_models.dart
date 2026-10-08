/// Mirrors Purch.Application.Catalog.ItemModifierDto. A category item offered
/// through a category-linked group is shown as one of these too, with
/// [isCategoryItem] set (its [id] is then the item's id).
class ItemModifierOption {
  const ItemModifierOption({
    required this.id,
    required this.name,
    required this.priceDelta,
    this.isOutOfStock = false,
    this.isCategoryItem = false,
  });

  factory ItemModifierOption.fromJson(Map<String, dynamic> json) {
    return ItemModifierOption(
      id: json['id'] as String,
      name: json['name'] as String,
      priceDelta: (json['priceDelta'] as num).toDouble(),
      isOutOfStock: json['isOutOfStock'] as bool? ?? false,
    );
  }

  final String id;
  final String name;
  final double priceDelta;
  final bool isOutOfStock;
  final bool isCategoryItem;
}

/// Mirrors Purch.Application.Catalog.ModifierCategoryItemDto — an item of the
/// group's linked category. [price] is what the customer pays (the group's
/// override, else the item's own); [isExcluded] items are hidden from ordering.
class ModifierCategoryItem {
  const ModifierCategoryItem({
    required this.itemId,
    required this.name,
    required this.basePrice,
    required this.price,
    this.priceOverride,
    this.isExcluded = false,
    this.isOutOfStock = false,
  });

  factory ModifierCategoryItem.fromJson(Map<String, dynamic> json) {
    return ModifierCategoryItem(
      itemId: json['itemId'] as String,
      name: json['name'] as String,
      basePrice: (json['basePrice'] as num).toDouble(),
      price: (json['price'] as num).toDouble(),
      priceOverride: (json['priceOverride'] as num?)?.toDouble(),
      isExcluded: json['isExcluded'] as bool? ?? false,
      isOutOfStock: json['isOutOfStock'] as bool? ?? false,
    );
  }

  final String itemId;
  final String name;
  final double basePrice;
  final double? priceOverride;
  final double price;
  final bool isExcluded;
  final bool isOutOfStock;
}

/// Mirrors Purch.Application.Catalog.ModifierGroupDto — always includes its
/// modifiers, since the backend never returns one without the other.
class ModifierGroup {
  const ModifierGroup({
    required this.id,
    required this.name,
    required this.allowMultipleSelection,
    required this.isRequired,
    required this.modifiers,
    this.categoryId,
    this.categoryItems = const [],
  });

  factory ModifierGroup.fromJson(Map<String, dynamic> json) {
    return ModifierGroup(
      id: json['id'] as String,
      name: json['name'] as String,
      allowMultipleSelection: json['allowMultipleSelection'] as bool,
      isRequired: json['isRequired'] as bool,
      modifiers:
          (json['modifiers'] as List<dynamic>)
              .cast<Map<String, dynamic>>()
              .map(ItemModifierOption.fromJson)
              .toList(),
      categoryId: json['categoryId'] as String?,
      categoryItems:
          (json['categoryItems'] as List<dynamic>?)
              ?.cast<Map<String, dynamic>>()
              .map(ModifierCategoryItem.fromJson)
              .toList() ??
          const [],
    );
  }

  final String id;
  final String name;
  final bool allowMultipleSelection;

  /// When true, checkout (Phase 4) must require a selection from this group
  /// before the item can be added to the cart — e.g. a coffee shop making
  /// sugar level mandatory rather than skippable.
  final bool isRequired;
  final List<ItemModifierOption> modifiers;

  /// When set, every active item of this category is offered in the group too.
  final String? categoryId;
  final List<ModifierCategoryItem> categoryItems;

  /// Everything the group offers: its own modifiers, then the linked
  /// category's items that the group has not hidden.
  List<ItemModifierOption> get options => [
    ...modifiers,
    for (final item in categoryItems)
      if (!item.isExcluded)
        ItemModifierOption(
          id: item.itemId,
          name: item.name,
          priceDelta: item.price,
          isOutOfStock: item.isOutOfStock,
          isCategoryItem: true,
        ),
  ];
}

/// Mirrors Purch.Application.Catalog.CreateModifierGroupRequest.
class CreateModifierGroupRequest {
  const CreateModifierGroupRequest({
    required this.name,
    required this.allowMultipleSelection,
    required this.isRequired,
    this.categoryId,
  });

  final String name;
  final bool allowMultipleSelection;
  final bool isRequired;
  final String? categoryId;

  Map<String, dynamic> toJson() => {
    'name': name,
    'allowMultipleSelection': allowMultipleSelection,
    'isRequired': isRequired,
    'categoryId': categoryId,
  };
}

/// Mirrors Purch.Application.Catalog.UpdateModifierGroupRequest — also how a
/// group's category is linked or unlinked (null unlinks).
class UpdateModifierGroupRequest {
  const UpdateModifierGroupRequest({
    required this.name,
    required this.allowMultipleSelection,
    required this.isRequired,
    required this.categoryId,
  });

  final String name;
  final bool allowMultipleSelection;
  final bool isRequired;
  final String? categoryId;

  Map<String, dynamic> toJson() => {
    'name': name,
    'allowMultipleSelection': allowMultipleSelection,
    'isRequired': isRequired,
    'categoryId': categoryId,
  };
}

/// Mirrors Purch.Application.Catalog.UpdateModifierCategoryItemRequest — a
/// group's own price for one category item, and/or hiding it from the group.
class UpdateModifierCategoryItemRequest {
  const UpdateModifierCategoryItemRequest({
    this.priceOverride,
    this.isExcluded = false,
  });

  final double? priceOverride;
  final bool isExcluded;

  Map<String, dynamic> toJson() => {
    'priceOverride': priceOverride,
    'isExcluded': isExcluded,
  };
}

/// Mirrors Purch.Application.Catalog.CreateItemModifierRequest.
class CreateItemModifierRequest {
  const CreateItemModifierRequest({
    required this.name,
    required this.priceDelta,
  });

  final String name;
  final double priceDelta;

  Map<String, dynamic> toJson() => {'name': name, 'priceDelta': priceDelta};
}

/// Mirrors Purch.Application.Catalog.AttachModifierGroupRequest — attaches an
/// existing (possibly shared) modifier group to a specific item, e.g.
/// attaching "Ice Level" to every cold drink.
/// Mirrors Purch.Application.Catalog.AttachModifierGroupToItemsRequest: gives a
/// group to every item of [categoryId], or to the [itemIds] chosen - one of the two.
class AttachModifierGroupToItemsRequest {
  const AttachModifierGroupToItemsRequest({this.categoryId, this.itemIds});

  final String? categoryId;
  final List<String>? itemIds;

  Map<String, dynamic> toJson() => {
    if (categoryId != null) 'categoryId': categoryId,
    if (itemIds != null) 'itemIds': itemIds,
  };
}

/// How many items got the group now, and how many already had it.
class AttachModifierGroupToItemsResult {
  const AttachModifierGroupToItemsResult({
    required this.attached,
    required this.alreadyAttached,
  });

  factory AttachModifierGroupToItemsResult.fromJson(Map<String, dynamic> json) {
    return AttachModifierGroupToItemsResult(
      attached: (json['attached'] as num).toInt(),
      alreadyAttached: (json['alreadyAttached'] as num).toInt(),
    );
  }

  final int attached;
  final int alreadyAttached;
}

class AttachModifierGroupRequest {
  const AttachModifierGroupRequest({required this.modifierGroupId});

  final String modifierGroupId;

  Map<String, dynamic> toJson() => {'modifierGroupId': modifierGroupId};
}
