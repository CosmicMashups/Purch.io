interface ErrorStateProps {
  title?: string;
  message?: string;
  onRetry?: () => void;
}

/** Shown in place of a list/table when its query failed, instead of silently falling through to the
 * empty state (which used to read "No items found" for a network error — indistinguishable from an
 * actually-empty catalog). `onRetry` re-runs the query without a full page reload. */
export function ErrorState({ title = 'Something went wrong', message, onRetry }: ErrorStateProps) {
  return (
    <div className="flex flex-col items-center justify-center gap-2 rounded-lg border border-danger bg-surface px-6 py-12 text-center">
      <p className="text-sm font-medium text-danger">{title}</p>
      {message && <p className="text-sm text-ink-soft">{message}</p>}
      {onRetry && (
        <button
          type="button"
          onClick={onRetry}
          className="mt-2 rounded-md bg-danger px-3 py-1.5 text-sm font-medium text-white hover:opacity-90"
        >
          Try again
        </button>
      )}
    </div>
  );
}

/** Extracts a short, user-safe message from an unknown query error — never the raw error object,
 * which can be an AxiosError with a huge, irrelevant JSON body. */
export function describeQueryError(error: unknown): string | undefined {
  if (error && typeof error === 'object' && 'message' in error && typeof error.message === 'string') {
    return error.message;
  }
  return undefined;
}
