import { afterEach, describe, expect, it, vi } from 'vitest';
import { apiClient } from '../../../lib/apiClient';
import { reportsApi } from '../api';

describe('inventoryCogs request', () => {
  afterEach(() => vi.restoreAllMocks());

  it('sends business days, inclusive end, and no branchId', async () => {
    const get = vi.spyOn(apiClient, 'get').mockResolvedValue({ data: {} });
    await reportsApi.inventoryCogs({ from: '2026-09-30T16:00:00.000Z', to: '2026-10-30T16:00:00.000Z', branchId: 'b1', groupBy: 'item' });
    const [url, config] = get.mock.calls[0];
    expect(apiClient.getUri({ url, params: config?.params })).toContain('/reports/inventory/cogs?from=2026-10-01&to=2026-10-30&groupBy=item');
    expect(config?.params).not.toHaveProperty('branchId');
  });
});
