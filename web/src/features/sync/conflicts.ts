import type { FlaggedSyncRecord } from '../dashboard/types';

/** Records still waiting for a person come first, then the reviewed ones; each group newest first. */
export function sortForReview(records: FlaggedSyncRecord[]): FlaggedSyncRecord[] {
  return [...records].sort((a, b) => {
    const aOpen = a.reviewedAt === null;
    const bOpen = b.reviewedAt === null;
    if (aOpen !== bOpen) return aOpen ? -1 : 1;
    return new Date(b.clientTimestamp).getTime() - new Date(a.clientTimestamp).getTime();
  });
}

export function openCount(records: FlaggedSyncRecord[]): number {
  return records.filter((r) => r.reviewedAt === null).length;
}

/** The record's id, shortened so two entries about the same record can be matched by eye. */
export function shortRecordId(id: string): string {
  return `#${id.replace(/-/g, '').slice(0, 8)}`;
}
