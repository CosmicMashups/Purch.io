/// These enums are sent/received as plain integers (not strings) because the
/// backend has no JsonStringEnumConverter configured — enum JSON values are
/// C#'s default ordinal encoding. Declaration order here MUST exactly match
/// backend/src/Purch.Domain/Enums/*.cs, or a value silently maps to the wrong
/// enum member instead of failing loudly.
library;

/// Mirrors Purch.Domain.Enums.BusinessType.
enum BusinessType {
  convenienceStore,
  restaurant,
  cafe,
  clothingShop,
  departmentStore,
  groceryStore,
  sariSariStore,
  serviceEstablishment,
  other,
}

/// Mirrors Purch.Domain.Enums.Role.
enum StaffRole { admin, manager, cashier, warehouse }

/// Mirrors Purch.Domain.Enums.ScopeType.
enum ScopeType { tenant, branch, department }
