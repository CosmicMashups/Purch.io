import { QueryClient } from '@tanstack/react-query';
import { renderHook } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { dashboardKeys, useRefreshDashboards } from './queries';

vi.mock('@tanstack/react-query', async () => {
  const actual = await vi.importActual<typeof import('@tanstack/react-query')>('@tanstack/react-query');
  return actual;
});

describe('useRefreshDashboards', () => {
  it('invalidates every dashboard key together, so a manual refresh or a sale refetches all of them', async () => {
    const client = new QueryClient();
    const invalidate = vi.spyOn(client, 'invalidateQueries').mockResolvedValue();
    const { QueryClientProvider } = await import('@tanstack/react-query');
    const { result } = renderHook(() => useRefreshDashboards(), {
      wrapper: ({ children }) => <QueryClientProvider client={client}>{children}</QueryClientProvider>,
    });

    await result.current();

    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['dashboard'] });
    // Every real dashboard key falls under this one root, so one invalidation reaches sales, inventory and flaggedSync.
    for (const key of Object.values(dashboardKeys)) expect(key[0]).toBe('dashboard');
  });
});
