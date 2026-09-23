import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/core/formatting/dates.dart';

void main() {
  test('formatDateTime shows the month, day, year and 12-hour time', () {
    expect(formatDateTime(DateTime.utc(2026, 9, 21, 7, 45).toLocal()), contains('2026'));
  });

  test('formatDate and formatTime split the same moment', () {
    final moment = DateTime(2026, 9, 21, 15, 30);
    expect(formatDate(moment), 'Sep 21, 2026');
    expect(formatTime(moment), '3:30 PM');
  });

  test('formatRelativeDateTime drops the date for today, keeps it otherwise', () {
    final now = DateTime(2026, 9, 21, 18, 0);
    expect(formatRelativeDateTime(DateTime(2026, 9, 21, 9, 5), now: now), '9:05 AM');
    expect(formatRelativeDateTime(DateTime(2026, 9, 20, 23, 59), now: now), 'Sep 20, 11:59 PM');
  });
}
