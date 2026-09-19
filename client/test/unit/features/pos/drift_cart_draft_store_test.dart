import 'package:drift/native.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/core/db/app_database.dart';
import 'package:purch_client/features/pos/data/drift_cart_draft_store.dart';

void main() {
  late AppDatabase database;

  DriftCartDraftStore storeFor() => DriftCartDraftStore(
    dao: database.localCartDraftDao,
    identityDao: database.deviceIdentityDao,
  );

  setUp(() => database = AppDatabase.forTesting(NativeDatabase.memory()));
  tearDown(() => database.close());

  test('persists and clears a draft', () async {
    await database.deviceIdentityDao.saveIdentity(
      deviceId: 'd1',
      tenantId: 't1',
      branchId: 'b1',
    );
    final store = storeFor();

    expect(await store.read(), isNull);
    await store.write('{"a":1}');
    expect(await store.read(), '{"a":1}');
    await store.write('{"a":2}');
    expect(await store.read(), '{"a":2}');
    await store.clear();
    expect(await store.read(), isNull);
  });

  test('a draft is never visible to another tenant on the same device', () async {
    await database.deviceIdentityDao.saveIdentity(
      deviceId: 'd1',
      tenantId: 'tenant-a',
      branchId: 'b1',
    );
    await storeFor().write('tenant A cart');

    await database.deviceIdentityDao.saveIdentity(
      deviceId: 'd1',
      tenantId: 'tenant-b',
      branchId: 'b2',
    );
    expect(await storeFor().read(), isNull);

    await database.deviceIdentityDao.saveIdentity(
      deviceId: 'd1',
      tenantId: 'tenant-a',
      branchId: 'b1',
    );
    expect(await storeFor().read(), 'tenant A cart');
  });
}
