import type { PersistedClient, Persister } from '@tanstack/react-query-persist-client';
import { CACHE_ROW_ID, offlineDb } from './db';

/** Storage can be missing or blocked (private windows, quota). Saving is a convenience, so it must never break the app. */
async function quietly<T>(work: () => Promise<T>, fallback: T): Promise<T> {
  try {
    return await work();
  } catch {
    return fallback;
  }
}

/**
 * Keeps the query cache in IndexedDB, for one business at a time. Data is written under the signed-in
 * business's id and only handed back to that same business; with nobody signed in, nothing is kept.
 */
export function createDexiePersister(getTenantId: () => string | null): Persister {
  return {
    persistClient: (client: PersistedClient) =>
      quietly(async () => {
        const tenantId = getTenantId();
        if (!tenantId) {
          await offlineDb.cache.delete(CACHE_ROW_ID);
          return;
        }
        await offlineDb.cache.put({ id: CACHE_ROW_ID, tenantId, savedAt: Date.now(), client });
      }, undefined),

    restoreClient: () =>
      quietly(async () => {
        const row = await offlineDb.cache.get(CACHE_ROW_ID);
        if (!row) return undefined;
        const tenantId = getTenantId();
        if (!tenantId || row.tenantId !== tenantId) {
          // Someone else's data, or nobody is signed in: it must not be shown, and is not worth keeping.
          await offlineDb.cache.delete(CACHE_ROW_ID);
          return undefined;
        }
        return row.client;
      }, undefined),

    removeClient: () => quietly(() => offlineDb.cache.delete(CACHE_ROW_ID), undefined),
  };
}

/** Wipes everything saved for offline use. Called on sign-out. */
export function clearOfflineCache(): Promise<void> {
  return quietly(() => offlineDb.cache.delete(CACHE_ROW_ID), undefined);
}
