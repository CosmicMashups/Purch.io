import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/core/auth/role_nav_policy.dart';
import 'package:purch_client/features/onboarding/domain/onboarding_enums.dart';

void main() {
  group('tabsForRole', () {
    test('gives Admin and Manager every tab, including Home', () {
      const all = [AppTab.home, AppTab.cashier, AppTab.inventory, AppTab.business];
      expect(tabsForRole(StaffRole.admin), all);
      expect(tabsForRole(StaffRole.manager), all);
    });

    test('confines a Cashier to the Cashier tab, with no Home', () {
      expect(tabsForRole(StaffRole.cashier), [AppTab.cashier]);
    });

    test('confines a Warehouse user to Inventory, with no Home', () {
      expect(tabsForRole(StaffRole.warehouse), [AppTab.inventory]);
    });

    test('falls back to the most restrictive set for an unknown role', () {
      expect(tabsForRole(null), [AppTab.cashier]);
    });

    test('keeps Home to Admin and Manager', () {
      for (final role in [StaffRole.cashier, StaffRole.warehouse, null]) {
        expect(tabsForRole(role), isNot(contains(AppTab.home)));
      }
    });
  });

  group('landing and allowed pages', () {
    test('lands each role on the first page it has', () {
      expect(landingPathForRole(StaffRole.admin), '/home');
      expect(landingPathForRole(StaffRole.manager), '/home');
      expect(landingPathForRole(StaffRole.cashier), '/cashier');
      expect(landingPathForRole(StaffRole.warehouse), '/inventory');
      expect(landingPathForRole(null), '/cashier');
    });

    test('lets a role open only the pages under its own tabs', () {
      expect(roleMayOpen(StaffRole.cashier, '/cashier/shift'), isTrue);
      expect(roleMayOpen(StaffRole.cashier, '/home'), isFalse);
      expect(roleMayOpen(StaffRole.cashier, '/inventory/items'), isFalse);
      expect(roleMayOpen(StaffRole.warehouse, '/inventory/movements'), isTrue);
      expect(roleMayOpen(StaffRole.warehouse, '/cashier'), isFalse);
      expect(roleMayOpen(StaffRole.manager, '/business/staff'), isTrue);
    });
  });
}
