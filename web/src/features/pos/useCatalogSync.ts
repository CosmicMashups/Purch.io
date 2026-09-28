import { useCallback, useEffect } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { catalogKeys } from '../catalog/queries';

/** How often an open register re-checks the catalog in the background. */
export const CATALOG_SYNC_INTERVAL_MS = 5 * 60_000;
/** Each register waits a random amount either side of the interval, so a shop's devices never all ask in the same second. */
export const CATALOG_SYNC_JITTER_MS = 30_000;

export const nextSyncDelay = (random: () => number = Math.random) => CATALOG_SYNC_INTERVAL_MS + (random() * 2 - 1) * CATALOG_SYNC_JITTER_MS;

/**
 * Keeps the catalog the register prices from current. Opening (or reloading) the register already shows the saved
 * copy at once and refetches it, because saved data counts as stale; on top of that, an open register re-checks every
 * few minutes, and `refresh` does it on demand so a price change can take effect immediately. The server's prices
 * always win at checkout, so this only keeps the device's preview close to them.
 */
export function useCatalogSync(enabled: boolean): { refresh: () => Promise<void> } {
  const qc = useQueryClient();

  const refresh = useCallback(async () => {
    // Items, modifier groups and everything under an item (variants, its modifier groups).
    await Promise.all([qc.invalidateQueries({ queryKey: catalogKeys.items }), qc.invalidateQueries({ queryKey: catalogKeys.modifierGroups })]);
  }, [qc]);

  useEffect(() => {
    if (!enabled) return;
    let timer: ReturnType<typeof setTimeout>;
    const schedule = () => {
      timer = setTimeout(() => {
        void refresh().finally(schedule);
      }, nextSyncDelay());
    };
    schedule();
    return () => clearTimeout(timer);
  }, [enabled, refresh]);

  return { refresh };
}
