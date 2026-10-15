import { toast } from '../../components/feedback/toastStore';
import { SecondaryButton } from '../../components/forms/FormField';
import { Pill } from '../../components/lists/QueryList';
import { QueryTable } from '../../components/lists/QueryTable';
import type { DataColumn } from '../../components/lists/DataTable';
import { PageHeader } from '../../components/PageHeader';
import { formatDateTime } from '../../lib/dates';
import { useFlaggedSync } from '../dashboard/queries';
import { shortRecordId, sortForReview } from './conflicts';
import { useAcknowledgeFlagged } from './queries';

type Conflict = NonNullable<ReturnType<typeof useFlaggedSync>['data']>[number];

export function SyncConflictsPage() {
  const flagged = useFlaggedSync(true);
  const acknowledge = useAcknowledgeFlagged();

  const columns: DataColumn<Conflict>[] = [
    {
      header: 'Record',
      sortValue: (r) => `${r.entityType} ${shortRecordId(r.entityId)}`,
      cell: (r) => <span className="font-medium text-gray-900">{r.entityType} {shortRecordId(r.entityId)}</span>,
    },
    {
      header: 'Recorded',
      className: 'whitespace-nowrap text-gray-600',
      sortValue: (r) => new Date(r.clientTimestamp),
      cell: (r) => formatDateTime(r.clientTimestamp),
    },
    {
      header: 'Status',
      sortValue: (r) => (r.reviewedAt ? `Reviewed ${r.reviewedAt}` : ''),
      cell: (r) =>
        r.reviewedAt ? (
          <div>
            <Pill tone="brand">Reviewed</Pill>
            <p className="mt-1 text-xs text-gray-500">{formatDateTime(r.reviewedAt)}</p>
          </div>
        ) : (
          <Pill tone="warn">Needs review</Pill>
        ),
    },
    {
      header: '',
      className: 'text-right',
      cell: (r) =>
        r.reviewedAt ? null : (
          <button
            type="button"
            disabled={acknowledge.isPending}
            onClick={() => acknowledge.mutate(r.id, { onSuccess: () => toast.success('Marked as reviewed') })}
            className="h-12 shrink-0 rounded-control border border-brand px-5 text-base font-semibold text-brand-strong hover:bg-brand-tint disabled:opacity-60"
          >
            Acknowledge
          </button>
        ),
    },
  ];

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Sync conflicts"
        subtitle="Changes from two devices that touched the same record. The later one won; the other is listed here so nothing is dropped silently."
        backTo={{ to: '/business', label: 'Business' }}
        action={
          <SecondaryButton type="button" disabled={flagged.isFetching} onClick={() => void flagged.refetch()}>
            {flagged.isFetching ? 'Checking...' : 'Check again'}
          </SecondaryButton>
        }
      />

      <QueryTable
        query={flagged}
        label="Sync conflicts"
        columns={columns}
        getId={(r) => r.id}
        transform={sortForReview}
        errorTitle="Sync conflicts could not be loaded"
        emptyMessage="No conflicts to review. Every device's changes are in step."
      />
    </div>
  );
}
