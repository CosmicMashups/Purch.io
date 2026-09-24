import Dexie, { type Table } from 'dexie';
import type { PersistedClient } from '@tanstack/react-query-persist-client';

export const CACHE_ROW_ID = 'query-cache';

export interface CacheRow {
  id: typeof CACHE_ROW_ID;
  /** Whose data this is. It is only ever shown back to the same business. */
  tenantId: string;
  savedAt: number;
  client: PersistedClient;
}

class OfflineDatabase extends Dexie {
  cache!: Table<CacheRow, string>;

  constructor() {
    super('purch-offline');
    this.version(1).stores({ cache: 'id' });
  }
}

export const offlineDb = new OfflineDatabase();
