import { describe, expect, it } from 'vitest';
import { selectableBranches } from './scope';

const branches = [
  { id: 'a', name: 'Katipunan', address: null },
  { id: 'b', name: 'Kamuning', address: null },
];

describe('selectableBranches', () => {
  it('limits a Branch-scoped account to its own branch', () => {
    expect(selectableBranches(branches, 'Branch', 'b').map((b) => b.id)).toEqual(['b']);
  });

  it('offers every branch to Tenant scope, Department scope and unknown scope', () => {
    expect(selectableBranches(branches, 'Tenant', null)).toHaveLength(2);
    expect(selectableBranches(branches, 'Department', 'd1')).toHaveLength(2);
    expect(selectableBranches(branches, null, null)).toHaveLength(2);
  });

  it('does not filter when a Branch scope has no id', () => {
    expect(selectableBranches(branches, 'Branch', null)).toHaveLength(2);
  });
});
