/// Mirrors Purch.Application.Catalog.ItemModifierDto.
class ItemModifierOption {
  const ItemModifierOption({
    required this.id,
    required this.name,
    required this.priceDelta,
  });

  factory ItemModifierOption.fromJson(Map<String, dynamic> json) {
    return ItemModifierOption(
      id: json['id'] as String,
      name: json['name'] as String,
      priceDelta: (json['priceDelta'] as num).toDouble(),
    );
  }

  final String id;
  final String name;
  final double priceDelta;
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
}

/// Mirrors Purch.Application.Catalog.CreateModifierGroupRequest.
class CreateModifierGroupRequest {
  const CreateModifierGroupRequest({
    required this.name,
    required this.allowMultipleSelection,
    required this.isRequired,
  });

  final String name;
  final bool allowMultipleSelection;
  final bool isRequired;

  Map<String, dynamic> toJson() => {
    'name': name,
    'allowMultipleSelection': allowMultipleSelection,
    'isRequired': isRequired,
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
class AttachModifierGroupRequest {
  const AttachModifierGroupRequest({required this.modifierGroupId});

  final String modifierGroupId;

  Map<String, dynamic> toJson() => {'modifierGroupId': modifierGroupId};
}
