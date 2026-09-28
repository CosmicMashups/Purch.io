import type { ReactNode } from 'react';
import type { UseQueryResult } from '@tanstack/react-query';
import { ErrorState } from '../../../components/ErrorState';
import { Skeleton } from '../../../components/Skeleton';
import { userMessage } from '../../../lib/apiError';

interface AsyncPanelProps<T> {
  title: string;
  subtitle?: string;
  query: UseQueryResult<T>;
  isEmpty?: (data: T) => boolean;
  emptyMessage: string;
  minHeight?: string;
  children: (data: T) => ReactNode;
}

/** A titled panel that owns the loading, error and empty states so every dashboard block behaves alike. */
export function AsyncPanel<T>({ title, subtitle, query, isEmpty, emptyMessage, minHeight = 'min-h-48', children }: AsyncPanelProps<T>) {
  let body: ReactNode;

  if (query.isPending) {
    body = (
      <div className={`flex flex-col gap-3 ${minHeight}`} aria-busy="true" aria-label={`Loading ${title}`}>
        <Skeleton className="h-6 w-1/3" />
        <Skeleton className="h-full min-h-28 w-full" />
      </div>
    );
  } else if (query.isError) {
    body = <ErrorState title={`${title} is unavailable`} message={userMessage(query.error)} onRetry={() => void query.refetch()} />;
  } else if (isEmpty?.(query.data)) {
    body = <p className={`flex items-center text-base text-ink-soft ${minHeight}`}>{emptyMessage}</p>;
  } else {
    body = children(query.data);
  }

  // A cached figure shown while a background refetch is on its way is still worth a timestamp, so nobody
  // reads it as this second's number. Nothing to show before the first answer ever arrives.
  const asOf = query.dataUpdatedAt > 0 ? new Date(query.dataUpdatedAt) : null;

  return (
    <section aria-labelledby={`panel-${title}`} className="rounded-panel border border-line bg-surface p-5">
      <div className="flex items-baseline justify-between gap-3">
        <h2 id={`panel-${title}`} className="text-lg font-semibold">
          {title}
        </h2>
        {asOf && (
          <p className="text-xs text-ink-soft">
            {query.isFetching ? 'Updating…' : `As of ${asOf.toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' })}`}
          </p>
        )}
      </div>
      {subtitle && <p className="text-sm text-ink-soft">{subtitle}</p>}
      <div className={`mt-4 transition-opacity ${query.isFetching && !query.isPending ? "opacity-60" : ""}`} aria-busy={query.isFetching}>
        {body}
      </div>
    </section>
  );
}
