import { MutationCache, QueryClient } from '@tanstack/react-query';
import { toast } from '../components/feedback/toastStore';
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
      retry: 1,
      staleTime: 10_000,
    },
  },
});
