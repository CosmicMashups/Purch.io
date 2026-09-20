import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/auth/jwt_claims.dart';
import '../../../auth/presentation/providers/auth_providers.dart';
import '../../domain/branch_models.dart';
import 'onboarding_providers.dart';

/// The branches an account may offer for an action that changes a branch's stock. A branch-scoped manager is
/// confined to their own branch by the server (anything else is refused with a 403), so their pickers offer only
/// that one instead of choices that can only fail. Tenant-scoped accounts see every branch; a department-scoped
/// account is narrowed by the server, which answers with a clear message.
List<Branch> branchesForScope(List<Branch> branches, StaffScope? scope) {
  if (scope == null || !scope.isBranch) {
    return branches;
  }
  return branches.where((branch) => branch.id == scope.id).toList();
}

/// The branches the signed-in account can act on — see [branchesForScope].
final selectableBranchesProvider = FutureProvider<List<Branch>>((ref) async {
  final branches = await ref.watch(branchListProvider.future);

  // Narrowing the list is a convenience — the server is what enforces the confinement — so a token that can't
  // be read simply means "don't narrow", never a failure to show the screen at all.
  StaffScope? scope;
  try {
    final token = await ref.watch(secureTokenStorageProvider).readAccessToken();
    scope = token == null ? null : staffScopeFromJwt(token);
  } on Object {
    scope = null;
  }
  return branchesForScope(branches, scope);
});
