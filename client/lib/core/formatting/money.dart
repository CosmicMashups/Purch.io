import 'package:intl/intl.dart';

/// Central currency formatting for the Philippine Peso — the one currency this app has ever shipped in
/// (see esc_pos_builder.dart and cfd_http_server.dart, which normalize/reproduce the ₱ sign for their own
/// output channels and are intentionally left alone by this file). Everywhere else that shows a peso
/// amount to a person should go through here rather than hand-rolling `'₱${x.toStringAsFixed(2)}'`, so a
/// future currency or locale change (grouping separators, decimal places) is a one-file edit.
///
/// A fixed `en_PH` locale, not `Intl.systemLocale`: this app runs on POS terminals whose device locale is
/// whatever the OS install set, not something a cashier chose to control receipt formatting.
final NumberFormat _currency = NumberFormat.currency(locale: 'en_PH', symbol: '₱');
final NumberFormat _currencyNoDecimals = NumberFormat.currency(locale: 'en_PH', symbol: '₱', decimalDigits: 0);

/// e.g. `₱1,234.50`. Accepts `num` since callers hold a mix of `double` (money) and occasionally `int`
/// (a whole-peso amount already rounded elsewhere).
String formatCurrency(num amount) => _currency.format(amount);

/// e.g. `-₱50.00` becomes `₱50.00` — for a context that already shows the sign or direction itself
/// (a "Discount" label, a red/green color), where a second minus sign would be redundant or confusing.
String formatCurrencyAbs(num amount) => _currency.format(amount.abs());

/// e.g. `₱12k`, `₱1.2M` — for compact spaces (chart axis labels) where the exact figure lives in a
/// tooltip or nearby. Matches core/widgets/charts/chart_theme.dart's compactCurrency, which callers with
/// a chart already use; this is for the same shape of value outside chart code.
String formatCurrencyCompact(num amount) {
  final sign = amount < 0 ? '-' : '';
  final abs = amount.abs();
  if (abs >= 1000000) {
    return '$sign₱${(abs / 1000000).toStringAsFixed(abs >= 10000000 ? 0 : 1)}M';
  }
  if (abs >= 1000) {
    return '$sign₱${(abs / 1000).toStringAsFixed(abs >= 10000 ? 0 : 1)}k';
  }
  return _currencyNoDecimals.format(amount);
}
