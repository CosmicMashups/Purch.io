import { QueryClient, QueryClientProvider, useQuery } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import type { ReactElement } from 'react';
import { describe, expect, it } from 'vitest';
import { AsyncPanel } from './AsyncPanel';

/** Renders AsyncPanel driven by a real query, so `dataUpdatedAt`/`isFetching` come from TanStack Query itself. */
function withQuery(queryKey: unknown[], queryFn: () => Promise<string>, render_: (panel: ReactElement) => ReactElement = (p) => p) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  function Panel() {
    const query = useQuery({ queryKey, queryFn });
    return render_(<AsyncPanel title="Revenue" query={query} emptyMessage="Nothing yet">{(data) => <p>{data}</p>}</AsyncPanel>);
  }
  return render(
    <QueryClientProvider client={client}>
      <Panel />
    </QueryClientProvider>,
  );
}

describe('AsyncPanel', () => {
  it('shows no "as of" marker before the first answer ever arrives', () => {
    withQuery(['never'], () => new Promise(() => undefined));
    expect(screen.queryByText(/As of|Updating/)).not.toBeInTheDocument();
  });

  it('shows an "as of" time once data has loaded', async () => {
    withQuery(['loaded'], () => Promise.resolve('₱1,000'));
    expect(await screen.findByText(/As of \d/)).toBeInTheDocument();
    expect(await screen.findByText('₱1,000')).toBeInTheDocument();
  });
});
