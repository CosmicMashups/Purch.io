import { useState } from 'react';
import { useInfiniteQuery } from '@tanstack/react-query';
import { ErrorState } from '../../components/ErrorState';
import { SkeletonList } from '../../components/Skeleton';
import { FormField, SecondaryButton, controlClass } from '../../components/forms/FormField';
import { ListCard } from '../../components/lists/QueryList';
import { PageHeader } from '../../components/PageHeader';
import { userMessage } from '../../lib/apiError';
import { formatDateTime } from '../../lib/dates';
import { AUDIT_PAGE_SIZE, auditApi, auditDateBounds, shortId, type AuditCursor, type AuditFilter } from './audit';
import { useStaff } from './staffQueries';
import { AuditActionType, auditActionLabels, labelOf } from './types';

export function AuditLogPage() {
  const staff = useStaff();
  const [actor, setActor] = useState('');
  const [action, setAction] = useState('');
  const [fromDay, setFromDay] = useState('');
  const [toDay, setToDay] = useState('');

  const bounds = auditDateBounds(fromDay, toDay);
  const filter: AuditFilter | null = bounds.ok
    ? {
        ...(actor ? { actorUserId: actor } : {}),
        ...(action !== '' ? { actionType: Number(action) } : {}),
        ...(bounds.from ? { from: bounds.from } : {}),
        ...(bounds.to ? { to: bounds.to } : {}),
      }
    : null;

  const log = useInfiniteQuery({
    queryKey: ['audit-logs', filter],
    enabled: filter !== null,
    initialPageParam: null as AuditCursor | null,
    queryFn: ({ pageParam }) => auditApi.list(filter as AuditFilter, pageParam),
    getNextPageParam: (lastPage): AuditCursor | undefined => {
      const last = lastPage[lastPage.length - 1];
      return lastPage.length === AUDIT_PAGE_SIZE && last ? { before: last.createdAt, beforeId: last.id } : undefined;
    },
  });
  const rows = log.data?.pages.flat() ?? [];
  const nameOf = (id: string) => staff.data?.find((s) => s.id === id)?.name ?? 'A former staff member';

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Audit log" subtitle="Sensitive actions: who did what, and when" backTo={{ to: '/business', label: 'Business' }} />

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <FormField label="Action">
          <select value={action} onChange={(e) => setAction(e.target.value)} className={controlClass}>
            <option value="">All actions</option>
            {Object.values(AuditActionType).map((t) => (
              <option key={t} value={t}>
                {auditActionLabels[t]}
              </option>
            ))}
          </select>
        </FormField>
        <FormField label="Staff member">
          <select value={actor} onChange={(e) => setActor(e.target.value)} className={controlClass}>
            <option value="">Everyone</option>
            {(staff.data ?? []).map((s) => (
              <option key={s.id} value={s.id}>
                {s.name}
              </option>
            ))}
          </select>
        </FormField>
        <FormField label="From">
          <input type="date" value={fromDay} onChange={(e) => setFromDay(e.target.value)} className={controlClass} />
        </FormField>
        <FormField label="To" error={bounds.ok ? undefined : bounds.message}>
          <input type="date" value={toDay} onChange={(e) => setToDay(e.target.value)} className={controlClass} />
        </FormField>
      </div>

      {filter === null && <p className="rounded-panel border border-dashed border-ink-soft/40 p-6 text-base text-ink-soft">Choose a valid date range to see the log.</p>}
      {filter !== null && log.isPending && <SkeletonList rows={6} />}
      {log.isError && <ErrorState title="The audit log could not be loaded" message={userMessage(log.error)} onRetry={() => void log.refetch()} />}
      {log.isSuccess && rows.length === 0 && <p className="rounded-panel border border-dashed border-ink-soft/40 p-6 text-base text-ink-soft">Nothing matches these filters.</p>}
      {log.isSuccess && rows.length > 0 && (
        <ul className="flex flex-col gap-3">
          {rows.map((row) => (
            <ListCard key={row.id}>
              <div className="min-w-0">
                <p className="text-base font-semibold">{labelOf(auditActionLabels, row.actionType)}</p>
                <p className="text-base">
                  {nameOf(row.actorUserId)}, {row.targetEntityType} {shortId(row.targetEntityId)}
                </p>
                <p className="text-sm text-ink-soft">{formatDateTime(row.createdAt)}</p>
              </div>
            </ListCard>
          ))}
        </ul>
      )}
      {log.hasNextPage && (
        <div>
          <SecondaryButton type="button" disabled={log.isFetchingNextPage} onClick={() => void log.fetchNextPage()}>
            {log.isFetchingNextPage ? 'Loading...' : 'Load older entries'}
          </SecondaryButton>
        </div>
      )}
    </div>
  );
}
