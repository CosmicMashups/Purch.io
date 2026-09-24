import { useQuery } from '@tanstack/react-query';
import { useSession } from '../auth/useSession';
import { branchesApi } from './api';
import { selectableBranches } from './scope';

export const branchKeys = { all: ['branches'] as const };

export const useBranches = () => useQuery({ queryKey: branchKeys.all, queryFn: branchesApi.list, staleTime: 5 * 60_000 });

/** Branches this account may pick when recording stock changes. */
export function useSelectableBranches() {
  const query = useBranches();
  const { claims } = useSession();
  const branches = query.data ? selectableBranches(query.data, claims?.scopeType ?? null, claims?.scopeId ?? null) : undefined;
  return { ...query, branches };
}
