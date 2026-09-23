import 'package:intl/intl.dart';

/// Central date/time formatting, in the device's local time zone (matching the many existing
/// `.toLocal()` call sites this replaces). Uses explicit patterns rather than a locale-driven skeleton —
/// there is nothing PH-specific about "Sep 21, 2026" — so no locale data needs initializing at startup.
final DateFormat _dateTime = DateFormat('MMM d, y · h:mm a');
final DateFormat _date = DateFormat('MMM d, y');
final DateFormat _time = DateFormat('h:mm a');

/// e.g. `Sep 21, 2026 · 3:45 PM`.
String formatDateTime(DateTime moment) => _dateTime.format(moment.toLocal());

/// e.g. `Sep 21, 2026`.
String formatDate(DateTime moment) => _date.format(moment.toLocal());

/// e.g. `3:45 PM`.
String formatTime(DateTime moment) => _time.format(moment.toLocal());

/// e.g. `3:45 PM` for today, `Sep 20, 3:45 PM` otherwise — for a list where "today" is the common case
/// and repeating today's date on every row would be noise. [now] is injectable for tests.
String formatRelativeDateTime(DateTime moment, {DateTime? now}) {
  final local = moment.toLocal();
  final today = (now ?? DateTime.now()).toLocal();
  final sameDay = local.year == today.year && local.month == today.month && local.day == today.day;
  return sameDay ? _time.format(local) : DateFormat('MMM d, h:mm a').format(local);
}
