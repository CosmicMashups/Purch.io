import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/core/errors/failure.dart';
import 'package:purch_client/features/catalog/domain/modifier_models.dart';
import 'package:purch_client/features/catalog/presentation/providers/catalog_providers.dart';

import '../../../helpers/fake_catalog_repository.dart';

class _CountingCatalog extends FakeCatalogRepository {
  int calls = 0;
  Object? failure;

  @override
  Future<List<ModifierGroup>> listModifierGroupsForItem(String itemId) async {
    calls++;
    final problem = failure;
    if (problem != null) {
      throw problem;
    }
    return super.listModifierGroupsForItem(itemId);
  }
}

void main() {
  late _CountingCatalog catalog;
  late ProviderContainer container;

  setUp(() {
    catalog = _CountingCatalog();
    container = ProviderContainer(
      overrides: [catalogRepositoryProvider.overrideWithValue(catalog)],
    );
    addTearDown(container.dispose);
  });

  test('a repeat tap on the same item does not ask the server again', () async {
    await container.read(itemModifierGroupListProvider('latte').future);
    await container.read(itemModifierGroupListProvider('latte').future);
    await container.read(itemModifierGroupListProvider('latte').future);
    expect(catalog.calls, 1);
  });

  test('each item is asked about once, separately', () async {
    await container.read(itemModifierGroupListProvider('latte').future);
    await container.read(itemModifierGroupListProvider('mocha').future);
    await container.read(itemModifierGroupListProvider('latte').future);
    expect(catalog.calls, 2);
  });

  test('a failed lookup is not remembered, so the next tap asks again', () async {
    catalog.failure = const NetworkFailure('offline');
    await expectLater(
      container.read(itemModifierGroupListProvider('latte').future),
      throwsA(isA<NetworkFailure>()),
    );
    await Future<void>.delayed(Duration.zero);

    catalog.failure = null;
    final groups = await container.read(
      itemModifierGroupListProvider('latte').future,
    );
    expect(groups, isEmpty);
    expect(catalog.calls, 2);
  });

  test('a refresh asks the server again', () async {
    await container.read(itemModifierGroupListProvider('latte').future);
    await container
        .read(itemModifierGroupListProvider('latte').notifier)
        .refresh();
    expect(catalog.calls, 2);
  });
}
