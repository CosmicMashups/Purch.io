import type { ReactNode } from 'react';
import type { UseQueryResult } from '@tanstack/react-query';
import { ErrorState } from '../ErrorState';
import { SkeletonList } from '../Skeleton';
import { userMessage } from '../../lib/apiError';

/** Loading, error and empty states for a list backed by a query, so every list behaves alike. */
export function QueryList<T>({
  query,
  errorTitle = 'Could not load this list',
  emptyMessage,
  renderRow,
  transform,
}: {
  query: UseQueryResult<T[]>;
  errorTitle?: string;
  emptyMessage: string;
  renderRow: (row: T) => ReactNode;
  /** Reorders or filters the rows for display without changing the cached data. */
  transform?: (rows: T[]) => T[];
}) {
  if (query.isPending) return <SkeletonList />;
  if (query.isError) {
    return <ErrorState title={errorTitle} message={userMessage(query.error)} onRetry={() => void query.refetch()} />;
  }
  const rows = transform ? transform(query.data) : query.data;
  if (rows.length === 0) {
    return <p className="rounded-panel border border-dashed border-ink-soft/40 p-6 text-base text-ink-soft">{emptyMessage}</p>;
  }
  return <ul className="flex flex-col gap-3">{rows.map(renderRow)}</ul>;
}

export function ListCard({ children }: { children: ReactNode }) {
  return <li className="flex items-start justify-between gap-4 rounded-panel border border-line bg-surface p-4">{children}</li>;
}

const PILL_TONE = {
  brand: 'bg-brand-tint text-brand-strong',
  neutral: 'bg-line text-ink-soft',
  warn: 'bg-amber-100 text-amber-900',
  danger: 'bg-red-100 text-red-900',
} as const;

export function Pill({ tone = 'neutral', children }: { tone?: keyof typeof PILL_TONE; children: ReactNode }) {
  return <span className={`shrink-0 rounded-full px-3 py-1 text-sm font-semibold ${PILL_TONE[tone]}`}>{children}</span>;
}
