import { apiClient } from '../../lib/apiClient';
import { dayRangeToUtc } from '../reports/range';

/** Mirrors Purch.Application.Onboarding.AuditLogDto. */
export interface AuditEntry {
  id: string;
  actorUserId: string;
  actionType: number;
  targetEntityType: string;
  targetEntityId: string;
  createdAt: string;
}

export interface AuditFilter {
  actorUserId?: string;
  actionType?: number;
  from?: string;
  to?: string;
}

export interface AuditCursor {
  before: string;
  beforeId: string;
}

export const AUDIT_PAGE_SIZE = 30;

export const auditApi = {
  list: (filter: AuditFilter, cursor: AuditCursor | null) =>
    apiClient
      .get<AuditEntry[]>('/audit-logs', { params: { ...filter, limit: AUDIT_PAGE_SIZE, before: cursor?.before, beforeId: cursor?.beforeId } })
      .then((r) => r.data),
};

/**
 * Optional whole-day bounds in business (Manila) time. Either end may be left open; when both are set the
 * end must not be before the start.
 */
export function auditDateBounds(fromDay: string, toDay: string): { ok: true; from?: string; to?: string } | { ok: false; message: string } {
  if (fromDay && toDay && toDay < fromDay) return { ok: false, message: 'The end date is before the start date' };
  const bounds: { from?: string; to?: string } = {};
  if (fromDay) bounds.from = dayRangeToUtc({ fromDay, toDay: fromDay }).from;
  if (toDay) bounds.to = dayRangeToUtc({ fromDay: toDay, toDay }).to;
  return { ok: true, ...bounds };
}

/** A short, stable label for an entity id, so two entries about the same record can be matched by eye. */
export function shortId(id: string): string {
  return `#${id.replace(/-/g, '').slice(0, 8)}`;
}
