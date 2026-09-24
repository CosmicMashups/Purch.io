import { MutationCache, QueryClient } from '@tanstack/react-query';
import { toast } from '../components/feedback/toastStore';
import { CACHE_MAX_AGE_MS } from '../offline/db/cachePolicy';
import { ApiError, userMessage } from './apiError';

declare module '@tanstack/react-query' {
  interface Register {
    mutationMeta: { silent?: boolean };
  }
}

/** Any failed save tells the user why. A mutation that reports its own failure sets `meta: { silent: true }`. */
export function notifyMutationError(error: unknown, silent: boolean | undefined): void {
  if (silent) return;
  // An expired session sends the user to sign-in; a toast on top of that is noise.
  if (error instanceof ApiError && error.kind === 'unauthorized') return;
  toast.error(userMessage(error));
}

export const queryClient = new QueryClient({
  mutationCache: new MutationCache({
    onError: (error, _variables, _context, mutation) => notifyMutationError(error, mutation.meta?.silent),
  }),
  defaultOptions: {
    queries: {
      // A failure that says nothing about the request itself (no connection, a cold-starting or briefly
      // unreachable backend, an expired token that is being renewed) is retried with growing pauses, so
      // dashboards and charts recover on their own instead of staying in an error state until a reload.
      // Failures the server meant (validation, permissions, not found) are not worth repeating.
      retry: (failureCount, error) => {
        if (error instanceof ApiError) {
          const transient = ['network', 'serviceUnavailable', 'unknown', 'unauthorized', 'rateLimited'];
          if (!transient.includes(error.kind)) return false;
          if (error.kind === 'rateLimited') return failureCount < 2;
        }
        return failureCount < 4;
      },
      retryDelay: (attempt) => Math.min(1000 * 2 ** attempt, 15_000),
      refetchOnReconnect: 'always',
      staleTime: 10_000,
      // Kept in memory as long as it may be saved offline, or a restored copy would be discarded early.
      gcTime: CACHE_MAX_AGE_MS,
    },
    mutations: {
      // The default would pause a save while offline and replay it on reconnect, possibly long after the
      // person has moved on. There is no offline write queue, so a save must fail now and say so.
      networkMode: 'always',
    },
  },
});
