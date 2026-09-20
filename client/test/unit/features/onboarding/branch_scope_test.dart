import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/core/auth/jwt_claims.dart';
import 'package:purch_client/features/onboarding/domain/branch_models.dart';
import 'package:purch_client/features/onboarding/domain/hardware_enums.dart';
import 'package:purch_client/features/onboarding/presentation/providers/branch_scope_providers.dart';

import 'dart:convert';

Branch _branch(String id, String name) => Branch(
  id: id,
  name: name,
  address: null,
  receiptPrinterProfile: ReceiptPrinterProfile.values.first,
  cashDrawerEnabled: false,
  cashDrawerPolicy: CashDrawerPolicy.values.first,
  manualGcashQrImageUrl: null,
  manualGcashAccountName: null,
  manualGcashAccountNumber: null,
);

String _token(Map<String, dynamic> claims) {
  String part(Object o) => base64Url.encode(utf8.encode(jsonEncode(o))).replaceAll('=', '');
  return '${part({'alg': 'none'})}.${part(claims)}.sig';
}

void main() {
  final main = _branch('b-main', 'Main');
  final second = _branch('b-2', 'Second');

  group('staffScopeFromJwt', () {
    test('reads the scope type and id', () {
      final scope = staffScopeFromJwt(_token({'scope_type': 'Branch', 'scope_id': 'b-2'}))!;
      expect(scope.isBranch, isTrue);
      expect(scope.id, 'b-2');
    });

    test('a tenant scope, a missing claim, or a garbage token is not a branch scope', () {
      expect(staffScopeFromJwt(_token({'scope_type': 'Tenant'}))!.isBranch, isFalse);
      expect(staffScopeFromJwt(_token({'role': 'Cashier'})), isNull);
      expect(staffScopeFromJwt('not-a-jwt'), isNull);
    });
  });

  group('branchesForScope', () {
    test('a branch-scoped account is offered only its own branch', () {
      final scope = staffScopeFromJwt(_token({'scope_type': 'Branch', 'scope_id': 'b-2'}));
      expect(branchesForScope([main, second], scope), [second]);
    });

    test('a tenant-scoped account, or none, is offered every branch', () {
      final tenant = staffScopeFromJwt(_token({'scope_type': 'Tenant'}));
      expect(branchesForScope([main, second], tenant), [main, second]);
      expect(branchesForScope([main, second], null), [main, second]);
    });
  });
}
