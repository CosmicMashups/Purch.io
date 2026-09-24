import 'fake-indexeddb/auto';
import type { PersistedClient } from '@tanstack/react-query-persist-client';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { CACHE_ROW_ID, offlineDb } from './db';
import { clearOfflineCache, createDexiePersister } from './persister';

const snapshot = (label: string): PersistedClient => ({
  timestamp: 1,
  buster: 'v1',
  clientState: { mutations: [], queries: [{ queryKey: ['items'], queryHash: '["items"]', dehydratedAt: 1, state: { data: [{ id: label }] } as never }] },
});

beforeEach(async () => {
  await offlineDb.cache.clear();
});
afterEach(() => vi.restoreAllMocks());

describe('createDexiePersister', () => {
  it('gives a business its own saved data back', async () => {
    const persister = createDexiePersister(() => 'tenant-a');
    await persister.persistClient(snapshot('A'));
    expect(await persister.restoreClient()).toEqual(snapshot('A'));
  });

  it("never shows one business another business's data, and throws the data away", async () => {
    await createDexiePersister(() => 'tenant-a').persistClient(snapshot('A'));
    const asB = createDexiePersister(() => 'tenant-b');
    expect(await asB.restoreClient()).toBeUndefined();
    expect(await offlineDb.cache.get(CACHE_ROW_ID)).toBeUndefined();
  });

  it('shows nothing when nobody is signed in, and deletes what was saved', async () => {
    await createDexiePersister(() => 'tenant-a').persistClient(snapshot('A'));
    expect(await createDexiePersister(() => null).restoreClient()).toBeUndefined();
    expect(await offlineDb.cache.get(CACHE_ROW_ID)).toBeUndefined();
  });

  it('keeps nothing when it is asked to save with nobody signed in', async () => {
    await createDexiePersister(() => 'tenant-a').persistClient(snapshot('A'));
    await createDexiePersister(() => null).persistClient(snapshot('B'));
    expect(await offlineDb.cache.get(CACHE_ROW_ID)).toBeUndefined();
  });

  it('replaces the previous snapshot rather than piling up', async () => {
    const persister = createDexiePersister(() => 'tenant-a');
    await persister.persistClient(snapshot('one'));
    await persister.persistClient(snapshot('two'));
    expect(await offlineDb.cache.count()).toBe(1);
    expect(await persister.restoreClient()).toEqual(snapshot('two'));
  });

  it('removes the client on request', async () => {
    const persister = createDexiePersister(() => 'tenant-a');
    await persister.persistClient(snapshot('A'));
    await persister.removeClient();
    expect(await persister.restoreClient()).toBeUndefined();
  });

  it('never throws when storage is unavailable', async () => {
    vi.spyOn(offlineDb.cache, 'put').mockRejectedValue(new Error('QuotaExceededError'));
    vi.spyOn(offlineDb.cache, 'get').mockRejectedValue(new Error('blocked'));
    vi.spyOn(offlineDb.cache, 'delete').mockRejectedValue(new Error('blocked'));
    const persister = createDexiePersister(() => 'tenant-a');
    await expect(persister.persistClient(snapshot('A'))).resolves.toBeUndefined();
    await expect(persister.restoreClient()).resolves.toBeUndefined();
    await expect(persister.removeClient()).resolves.toBeUndefined();
    await expect(clearOfflineCache()).resolves.toBeUndefined();
  });
});

describe('clearOfflineCache', () => {
  it('wipes what was saved', async () => {
    await createDexiePersister(() => 'tenant-a').persistClient(snapshot('A'));
    await clearOfflineCache();
    expect(await offlineDb.cache.count()).toBe(0);
  });
});
