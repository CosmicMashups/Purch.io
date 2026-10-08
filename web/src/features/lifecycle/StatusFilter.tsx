import { useDeletedRecords, useLifecycle } from './useLifecycle';
import type { LifecycleKind } from './api';
import { EmptyState } from '../../components/EmptyState';
import { ErrorState, describeQueryError } from '../../components/ErrorState';
import { SkeletonList } from '../../components/Skeleton';
import { formatDateTime } from '../../lib/dates';

export type StatusView = 'active' | 'inactive' | 'deleted';

const OPTIONS: { value: StatusView; label: string }[] = [
  { value: 'active', label: 'Active' },
  { value: 'inactive', label: 'Inactive' },
  { value: 'deleted', label: 'Deleted' },
];

/** Active / Inactive / Deleted, as one control a thumb can hit. Deleted is left out for people who can't restore. */
export function StatusFilter({ value, onChange, canSeeDeleted = true }: { value: StatusView; onChange: (next: StatusView) => void; canSeeDeleted?: boolean }) {
  const options = canSeeDeleted ? OPTIONS : OPTIONS.filter((o) => o.value !== 'deleted');
  return (
    <div role="group" aria-label="Show" className="inline-flex rounded-control border border-line bg-surface p-1">
      {options.map((option) => (
        <button
          key={option.value}
          type="button"
          aria-pressed={value === option.value}
          onClick={() => onChange(option.value)}
          className={`h-10 min-w-24 rounded-control px-4 text-base font-semibold ${value === option.value ? 'bg-brand text-on-brand' : 'text-ink-soft hover:text-ink'}`}
        >
          {option.label}
        </button>
      ))}
    </div>
  );
}

/** The Deleted view of a list: what was removed, when, and a Restore button. Restored records come back inactive. */
export function DeletedRecordsPanel({ kind, noun }: { kind: LifecycleKind; noun: string }) {
  const deleted = useDeletedRecords(kind, true);
  const { run, dialog } = useLifecycle();

  if (deleted.isPending) return <SkeletonList />;
  if (deleted.isError) return <ErrorState message={describeQueryError(deleted.error)} onRetry={() => void deleted.refetch()} />;
  if (deleted.data.length === 0) return <EmptyState title={`No deleted ${noun}`} />;

  return (
    <>
      <ul className="flex flex-col gap-2" aria-label={`Deleted ${noun}`}>
        {deleted.data.map((record) => (
          <li key={record.id} className="flex items-center justify-between gap-3 rounded-panel border border-line bg-surface p-4">
            <div className="min-w-0">
              <p className="truncate text-base font-semibold">{record.name}</p>
              {record.deletedAt && <p className="text-sm text-ink-soft">Deleted {formatDateTime(record.deletedAt)}</p>}
            </div>
            <button
              type="button"
              aria-label={`Restore ${record.name}`}
              onClick={() => run({ kind, id: record.id, name: record.name }, 'restore')}
              className="h-12 shrink-0 rounded-control border border-line px-5 text-base font-semibold hover:border-brand"
            >
              Restore
            </button>
          </li>
        ))}
      </ul>
      {dialog}
    </>
  );
}
