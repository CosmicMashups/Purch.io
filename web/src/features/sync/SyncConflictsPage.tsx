import { toast } from '../../components/feedback/toastStore';
import { SecondaryButton } from '../../components/forms/FormField';
import { ListCard, Pill, QueryList } from '../../components/lists/QueryList';
import { PageHeader } from '../../components/PageHeader';
import { formatDateTime } from '../../lib/dates';
import { useFlaggedSync } from '../dashboard/queries';
import { shortRecordId, sortForReview } from './conflicts';
import { useAcknowledgeFlagged } from './queries';

export function SyncConflictsPage() {
  const flagged = useFlaggedSync(true);
  const acknowledge = useAcknowledgeFlagged();

  return (
    <div className="flex max-w-3xl flex-col gap-6">
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

      <QueryList
        query={flagged}
        transform={sortForReview}
        errorTitle="Sync conflicts could not be loaded"
        emptyMessage="No conflicts to review. Every device's changes are in step."
        renderRow={(record) => {
          const reviewed = record.reviewedAt !== null;
          return (
            <ListCard key={record.id}>
              <div className="min-w-0">
                <p className="text-base font-semibold">
                  {record.entityType} {shortRecordId(record.entityId)}
                </p>
                <p className="text-base text-ink-soft">Lost the sync race. Recorded {formatDateTime(record.clientTimestamp)}.</p>
                {reviewed && record.reviewedAt && <p className="text-sm text-ink-soft">Reviewed {formatDateTime(record.reviewedAt)}</p>}
              </div>
              {reviewed ? (
                <Pill tone="brand">Reviewed</Pill>
              ) : (
                <button
                  type="button"
                  disabled={acknowledge.isPending}
                  onClick={() => acknowledge.mutate(record.id, { onSuccess: () => toast.success('Marked as reviewed') })}
                  className="h-12 shrink-0 rounded-control border border-brand px-5 text-base font-semibold text-brand-strong hover:bg-brand-tint disabled:opacity-60"
                >
                  Acknowledge
                </button>
              )}
            </ListCard>
          );
        }}
      />
    </div>
  );
}
