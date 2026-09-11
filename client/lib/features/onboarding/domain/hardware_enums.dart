/// Sent/received as plain integers — see onboarding_enums.dart for why.
/// Mirrors Purch.Domain.Enums.ReceiptPrinterProfile and CashDrawerPolicy.
library;

enum ReceiptPrinterProfile { none, thermalEscPos }

enum CashDrawerPolicy { kickOnSaleOnly, allowManualOpenWithManagerOverride }
