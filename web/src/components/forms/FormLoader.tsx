import type { ReactNode } from 'react';
import { ErrorState } from '../ErrorState';
import { Skeleton } from '../Skeleton';
import { userMessage } from '../../lib/apiError';

/**
 * Holds a form back until the data its defaults depend on has loaded. A form that mounts early
 * captures empty defaults and never picks the loaded values up.
 */
export function FormLoader({ ready, failed, children }: { ready: boolean; failed: { error: unknown; refetch: () => unknown } | null; children: ReactNode }) {
  if (failed) {
    return <ErrorState title="The form could not be loaded" message={userMessage(failed.error)} onRetry={() => void failed.refetch()} />;
  }
  if (!ready) {
    return (
      <div className="flex flex-col gap-3 rounded-panel border border-line bg-surface p-5" aria-busy="true">
        <Skeleton className="h-6 w-1/2" />
        <Skeleton className="h-12 w-full" />
        <Skeleton className="h-12 w-full" />
      </div>
    );
  }
  return <>{children}</>;
}
