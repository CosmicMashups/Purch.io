/// Mirrors Purch.Application.Catalog.ItemComboComponentDto.
class ItemComboComponent {
  const ItemComboComponent({
    required this.id,
    required this.componentCategoryId,
    required this.componentCategoryName,
    required this.slotLabel,
    required this.quantity,
    required this.substitutionUpchargeAmount,
  });

  factory ItemComboComponent.fromJson(Map<String, dynamic> json) {
    return ItemComboComponent(
      id: json['id'] as String,
      componentCategoryId: json['componentCategoryId'] as String,
      componentCategoryName: json['componentCategoryName'] as String,
      slotLabel: json['slotLabel'] as String,
      quantity: json['quantity'] as int,
      substitutionUpchargeAmount:
          (json['substitutionUpchargeAmount'] as num?)?.toDouble(),
    );
  }

  final String id;
  final String componentCategoryId;
  final String componentCategoryName;
  final String slotLabel;
  final int quantity;
  final double? substitutionUpchargeAmount;
}

/// Mirrors Purch.Application.Catalog.CreateItemComboComponentRequest — e.g. a
/// "Value Meal" combo's "Choose a Drink" slot: componentCategoryId points at
/// the Drinks category, quantity 1, substitutionUpchargeAmount set only if
/// picking outside the included tier costs extra.
class CreateItemComboComponentRequest {
  const CreateItemComboComponentRequest({
    required this.componentCategoryId,
    required this.slotLabel,
    required this.quantity,
    this.substitutionUpchargeAmount,
  });

  final String componentCategoryId;
  final String slotLabel;
  final int quantity;
  final double? substitutionUpchargeAmount;

  Map<String, dynamic> toJson() => {
    'componentCategoryId': componentCategoryId,
    'slotLabel': slotLabel,
    'quantity': quantity,
    'substitutionUpchargeAmount': substitutionUpchargeAmount,
  };
}
