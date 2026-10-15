import { useMemo, useState, type ReactNode } from 'react';
import type { UseQueryResult } from '@tanstack/react-query';
import { EmptyState } from '../../../components/EmptyState';
import { ErrorState } from '../../../components/ErrorState';
import { PageHeader } from '../../../components/PageHeader';
import { SearchBar } from '../../../components/SearchBar';
import { SkeletonList } from '../../../components/Skeleton';
import { DataTable, type DataColumn } from '../../../components/lists/DataTable';
import { userMessage } from '../../../lib/apiError';

export interface Fact {
  label: string;
  value: ReactNode;
}

/**
 * The frame of a record's own page: loads the list it comes from, finds the record, and shows a header,
 * a grid of facts and whatever the page puts below. Used by purchase requests, orders and delivery reports.
 */
export function RecordDetail<T extends { id: string }>({
  query,
  id,
  notFound,
  backTo,
  title,
  subtitle,
  action,
  facts,
  children,
}: {
  query: UseQueryResult<T[]>;
  id: string | undefined;
  notFound: string;
  backTo: { to: string; label: string };
  title: (record: T) => string;
  subtitle?: (record: T) => string | undefined;
  action?: (record: T) => ReactNode;
  facts: (record: T) => Fact[];
  children: (record: T) => ReactNode;
}) {
  if (query.isPending) return <SkeletonList />;
  if (query.isError) return <ErrorState title="This record could not be loaded" message={userMessage(query.error)} onRetry={() => void query.refetch()} />;
  const record = query.data.find((r) => r.id === id);
  if (!record) {
    return (
      <div className="flex flex-col gap-6">
        <PageHeader title="Not found" backTo={backTo} />
        <EmptyState title={notFound} />
      </div>
    );
  }
  return (
    <div className="flex flex-col gap-6">
      <PageHeader title={title(record)} subtitle={subtitle?.(record)} backTo={backTo} action={action?.(record)} />
      <dl className="grid gap-x-8 gap-y-4 rounded-panel border border-line bg-surface p-5 sm:grid-cols-2 lg:grid-cols-3">
        {facts(record).map((fact) => (
          <div key={fact.label}>
            <dt className="text-sm text-ink-soft">{fact.label}</dt>
            <dd className="text-base font-semibold">{fact.value || <span className="font-normal text-ink-soft">None</span>}</dd>
          </div>
        ))}
      </dl>
      {children(record)}
    </div>
  );
}

/** The lines of a record as a sortable, searchable table, with a count so a long record is easy to size up. */
export function LinesTable<L>({
  lines,
  columns,
  getId,
  nameOf,
  label,
  footer,
}: {
  lines: L[];
  columns: DataColumn<L>[];
  getId: (line: L) => string;
  nameOf: (line: L) => string;
  label: string;
  footer?: ReactNode;
}) {
  const [search, setSearch] = useState('');
  const shown = useMemo(() => {
    const q = search.trim().toLowerCase();
    return q ? lines.filter((l) => nameOf(l).toLowerCase().includes(q)) : lines;
    // nameOf is a stable accessor supplied inline by the page.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [lines, search]);

  return (
    <section aria-label={label} className="flex flex-col gap-3">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="text-xl font-bold tracking-tight">
          {label} <span className="text-base font-normal text-ink-soft">({lines.length})</span>
        </h2>
        {lines.length > 8 && <SearchBar value={search} onChange={setSearch} placeholder="Search items…" />}
      </div>
      {shown.length === 0 ? (
        <p className="rounded-panel border border-dashed border-ink-soft/40 p-6 text-base text-ink-soft">No items match this search.</p>
      ) : (
        <DataTable label={label} rows={shown} columns={columns} getId={getId} />
      )}
      {footer}
    </section>
  );
}
