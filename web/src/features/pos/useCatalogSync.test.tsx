import { renderHook } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { CATALOG_SYNC_INTERVAL_MS, CATALOG_SYNC_JITTER_MS, nextSyncDelay, useCatalogSync } from './useCatalogSync';

describe('nextSyncDelay', () => {
  it('stays within the jitter window around the interval', () => {
    expect(nextSyncDelay(() => 0)).toBe(CATALOG_SYNC_INTERVAL_MS - CATALOG_SYNC_JITTER_MS);
    expect(nextSyncDelay(() => 0.5)).toBe(CATALOG_SYNC_INTERVAL_MS);
    expect(nextSyncDelay(() => 1)).toBe(CATALOG_SYNC_INTERVAL_MS + CATALOG_SYNC_JITTER_MS);
  });
});

describe('useCatalogSync', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  const setup = (enabled: boolean) => {
    const client = new QueryClient();
    const invalidate = vi.spyOn(client, 'invalidateQueries').mockResolvedValue();
    const wrapper = ({ children }: { children: ReactNode }) => <QueryClientProvider client={client}>{children}</QueryClientProvider>;
    const hook = renderHook(() => useCatalogSync(enabled), { wrapper });
    return { invalidate, hook };
  };

  it('re-checks the catalog in the background after the interval, and again after that', async () => {
    const { invalidate } = setup(true);
    expect(invalidate).not.toHaveBeenCalled();

    await vi.advanceTimersByTimeAsync(CATALOG_SYNC_INTERVAL_MS + CATALOG_SYNC_JITTER_MS);
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['items'] });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['modifierGroups'] });
    const afterFirst = invalidate.mock.calls.length;

    await vi.advanceTimersByTimeAsync(CATALOG_SYNC_INTERVAL_MS + CATALOG_SYNC_JITTER_MS);
    expect(invalidate.mock.calls.length).toBeGreaterThan(afterFirst);
  });

  it('does nothing while disabled', async () => {
    const { invalidate } = setup(false);
    await vi.advanceTimersByTimeAsync(CATALOG_SYNC_INTERVAL_MS * 3);
    expect(invalidate).not.toHaveBeenCalled();
  });

  it('refreshes on demand', async () => {
    const { invalidate, hook } = setup(true);
    await hook.result.current.refresh();
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['items'] });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['modifierGroups'] });
  });
});
