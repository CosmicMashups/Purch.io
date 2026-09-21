import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/catalog/presentation/providers/catalog_providers.dart';
import 'package:purch_client/features/catalog/presentation/widgets/stale_catalog_banner.dart';

void main() {
  test('describeAsOf shows just the time for today and the date for earlier days', () {
    final now = DateTime(2026, 9, 21, 15, 0);

    expect(describeAsOf(DateTime(2026, 9, 21, 9, 5), now), '09:05');
    expect(describeAsOf(DateTime(2026, 9, 20, 23, 59), now), '20 Sep, 23:59');
  });

  testWidgets('says nothing while the catalog is current, and warns once it goes stale', (tester) async {
    final container = ProviderContainer();
    addTearDown(container.dispose);
    await tester.pumpWidget(
      UncontrolledProviderScope(
        container: container,
        child: const MaterialApp(home: Scaffold(body: StaleCatalogBanner())),
      ),
    );

    expect(find.textContaining('Offline'), findsNothing);

    container.read(catalogStaleSinceProvider.notifier).set(DateTime.now());
    await tester.pump();

    expect(find.textContaining('Prices and stock may be out of date'), findsOneWidget);
    expect(find.text('Refresh'), findsOneWidget);

    container.read(catalogStaleSinceProvider.notifier).set(null);
    await tester.pump();

    expect(find.textContaining('Offline'), findsNothing);
  });
}
