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
    required this.modifiers,
  });

  factory ModifierGroup.fromJson(Map<String, dynamic> json) {
    return ModifierGroup(
      id: json['id'] as String,
      name: json['name'] as String,
      allowMultipleSelection: json['allowMultipleSelection'] as bool,
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
  final List<ItemModifierOption> modifiers;
}

/// Mirrors Purch.Application.Catalog.CreateModifierGroupRequest.
class CreateModifierGroupRequest {
  const CreateModifierGroupRequest({
    required this.name,
    required this.allowMultipleSelection,
  });

  final String name;
  final bool allowMultipleSelection;

  Map<String, dynamic> toJson() => {
    'name': name,
    'allowMultipleSelection': allowMultipleSelection,
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
