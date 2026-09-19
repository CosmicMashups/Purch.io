import 'package:drift/native.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/core/db/app_database.dart';

void main() {
  group('DeviceIdentityDao', () {
    late AppDatabase database;

    setUp(() {
      database = AppDatabase.forTesting(NativeDatabase.memory());
    });

    tearDown(() async {
      await database.close();
    });

    test('getIdentity is null before any identity is saved', () async {
      final dao = database.deviceIdentityDao;
      expect(await dao.getIdentity(), isNull);
    });

    test('saveIdentity persists deviceId/tenantId/branchId', () async {
      final dao = database.deviceIdentityDao;

      await dao.saveIdentity(
        deviceId: 'device-1',
        tenantId: 'tenant-1',
        branchId: 'branch-1',
      );

      final identity = await dao.getIdentity();
      expect(identity?.deviceId, 'device-1');
      expect(identity?.tenantId, 'tenant-1');
      expect(identity?.branchId, 'branch-1');
      expect(identity?.lastKnownReceiptNumber, isNull);
    });

    test('saveIdentity on re-login replaces the previous row entirely', () async {
      final dao = database.deviceIdentityDao;

      await dao.saveIdentity(
        deviceId: 'device-1',
        tenantId: 'tenant-1',
        branchId: 'branch-1',
      );
      await dao.recordIssuedReceiptNumber(42);

      // A different device (or the same device re-paired onto a different
      // tenant/branch) logs in — the stale row must not linger.
      await dao.saveIdentity(
        deviceId: 'device-2',
        tenantId: 'tenant-2',
        branchId: 'branch-2',
      );

      final identity = await dao.getIdentity();
      expect(identity?.deviceId, 'device-2');
      expect(identity?.lastKnownReceiptNumber, isNull);
    });

    test('recordIssuedReceiptNumber updates the cached sequence in place', () async {
      final dao = database.deviceIdentityDao;
      await dao.saveIdentity(
        deviceId: 'device-1',
        tenantId: 'tenant-1',
        branchId: 'branch-1',
      );

      await dao.recordIssuedReceiptNumber(1);
      expect((await dao.getIdentity())?.lastKnownReceiptNumber, 1);

      await dao.recordIssuedReceiptNumber(2);
      expect((await dao.getIdentity())?.lastKnownReceiptNumber, 2);
    });

    test('recordIssuedReceiptNumber never moves the counter backwards', () async {
      final dao = database.deviceIdentityDao;
      await dao.saveIdentity(
        deviceId: 'device-1',
        tenantId: 'tenant-1',
        branchId: 'branch-1',
      );

      await dao.recordIssuedReceiptNumber(10);
      await dao.recordIssuedReceiptNumber(7);
      expect((await dao.getIdentity())?.lastKnownReceiptNumber, 10);
    });

    test('logging in again on the same device keeps its receipt counter', () async {
      final dao = database.deviceIdentityDao;
      await dao.saveIdentity(
        deviceId: 'device-1',
        tenantId: 'tenant-1',
        branchId: 'branch-1',
      );
      await dao.recordIssuedReceiptNumber(25);

      await dao.saveIdentity(
        deviceId: 'device-1',
        tenantId: 'tenant-1',
        branchId: 'branch-1',
      );

      expect((await dao.getIdentity())?.lastKnownReceiptNumber, 25);
    });

    test('recordIssuedReceiptNumber before any identity exists is a safe no-op', () async {
      final dao = database.deviceIdentityDao;
      await dao.recordIssuedReceiptNumber(1);
      expect(await dao.getIdentity(), isNull);
    });
  });
}
