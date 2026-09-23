import { Component, type ErrorInfo, type ReactNode } from 'react';

interface Props {
  children: ReactNode;
}

interface State {
  error: Error | null;
}

/**
 * Catches a render-time exception anywhere below it (a bad response shape, a bug in a page component)
 * and shows a recovery screen instead of the app going blank — React unmounts the whole tree on an
 * uncaught render error otherwise, with nothing on screen and nothing in the UI to explain why.
 * Query errors (network failures) are handled per-page by ErrorState instead; this is the last resort
 * for what neither a query's own error state nor a form's validation caught.
 */
export class ErrorBoundary extends Component<Props, State> {
  state: State = { error: null };

  static getDerivedStateFromError(error: Error): State {
    return { error };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    // No error-reporting service is wired up yet; the console is the only record until one is.
    console.error('Unhandled error in the admin UI:', error, info.componentStack);
  }

  private reset = () => {
    this.setState({ error: null });
  };

  render() {
    const { error } = this.state;
    if (!error) {
      return this.props.children;
    }

    return (
      <div className="flex min-h-screen flex-col items-center justify-center gap-3 bg-gray-50 px-6 text-center">
        <p className="text-lg font-semibold text-gray-900">Something went wrong</p>
        <p className="max-w-md text-sm text-gray-500">
          The page hit an unexpected error. You can try again, or reload if that doesn't help.
        </p>
        <div className="flex gap-2">
          <button
            type="button"
            onClick={this.reset}
            className="rounded-md bg-gray-900 px-4 py-2 text-sm font-medium text-white"
          >
            Try again
          </button>
          <button
            type="button"
            onClick={() => window.location.reload()}
            className="rounded-md px-4 py-2 text-sm font-medium text-gray-600 hover:bg-gray-100"
          >
            Reload page
          </button>
        </div>
      </div>
    );
  }
}
