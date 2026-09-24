import type { ScopeType } from '../../lib/jwt';
import type { Branch } from './types';

/**
 * Presentation only: a Branch-scoped account is offered just its own branch. The API refuses any other
 * branch regardless (BranchScopeGuard). Tenant and Department scopes see every branch here, because a
 * Department's branch is not in the token; the server still decides.
 */
export function selectableBranches(branches: Branch[], scopeType: ScopeType | null, scopeId: string | null): Branch[] {
  if (scopeType === 'Branch' && scopeId) return branches.filter((b) => b.id === scopeId);
  return branches;
}
