/// Sent/received as a plain integer — see onboarding_enums.dart for why.
/// Mirrors Purch.Domain.Enums.PricingType. Only `unit` has working
/// sub-resource UI in v1 (Phase 3's first slice); the others are selectable
/// here since the backend accepts them, but their dedicated screens
/// (variant matrix, combo builder, bundle rules, batches) aren't built yet.
enum PricingType { unit, weightVolume, bundle, service, combo, variantMatrix }

extension PricingTypeX on PricingType {
  String get label => switch (this) {
    PricingType.unit => 'Unit',
    PricingType.weightVolume => 'Weight / Volume',
    PricingType.bundle => 'Bundle',
    PricingType.service => 'Service',
    PricingType.combo => 'Combo',
    PricingType.variantMatrix => 'Variant Matrix',
  };
}

