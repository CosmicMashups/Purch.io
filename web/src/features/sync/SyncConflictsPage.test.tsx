import { fireEvent, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { useToastStore } from '../../components/feedback/toastStore';
import { ApiError } from '../../lib/apiError';
import { renderPage, signInAs } from '../../test/render';
import { dashboardApi } from '../dashboard/api';
import type { FlaggedSyncRecord } from '../dashboard/types';
import { syncApi } from './api';
import { SyncConflictsPage } from './SyncConflictsPage';

vi.mock('../dashboard/api', () => ({ dashboardApi: { flaggedSync: vi.fn(), sales: vi.fn(), inventory: vi.fn() } }));
vi.mock('./api', () => ({ syncApi: { acknowledge: vi.fn() } }));

const rec = (id: string, over: Partial<FlaggedSyncRecord> = {}): FlaggedSyncRecord => ({
  id,
  deviceId: 'dev',
  entityType: 'Sale',
  entityId: `3fa85f64-5717-4562-b3fc-2c963f66af${id}`,
  clientTimestamp: '2026-09-23T02:00:00Z',
  reviewedAt: null,
  ...over,
});

beforeEach(() => {
  vi.clearAllMocks();
  useToastStore.setState({ toasts: [] });
  signInAs('Manager');
  vi.mocked(dashboardApi.flaggedSync).mockResolvedValue([rec('11', { reviewedAt: '2026-09-24T00:00:00Z' }), rec('22')]);
  vi.mocked(syncApi.acknowledge).mockResolvedValue(undefined);
});

describe('SyncConflictsPage', () => {
  it('lists the record that needs review first, with a plain explanation', async () => {
    renderPage(<SyncConflictsPage />);
    const items = await screen.findAllByRole('listitem');
    expect(items[0]).toHaveTextContent('Sale #3fa85f64');
    expect(items[0]).toHaveTextContent('Lost the sync race');
    expect(items[0]).toContainElement(screen.getByRole('button', { name: 'Acknowledge' }));
    expect(items[1]).toHaveTextContent('Reviewed');
    expect(screen.getAllByRole('button', { name: 'Acknowledge' })).toHaveLength(1);
  });

  it('acknowledges a record and refreshes the list', async () => {
    renderPage(<SyncConflictsPage />);
    fireEvent.click(await screen.findByRole('button', { name: 'Acknowledge' }));
    await waitFor(() => expect(syncApi.acknowledge).toHaveBeenCalledWith('22'));
    await waitFor(() => expect(dashboardApi.flaggedSync).toHaveBeenCalledTimes(2));
  });

  it('shows the server message and keeps the record open when acknowledging fails', async () => {
    vi.mocked(syncApi.acknowledge).mockRejectedValue(new ApiError('forbidden', 'nope'));
    renderPage(<SyncConflictsPage />);
    fireEvent.click(await screen.findByRole('button', { name: 'Acknowledge' }));
    await waitFor(() => expect(useToastStore.getState().toasts[0]?.message).toBe('You do not have permission to do that.'));
    expect(screen.getByRole('button', { name: 'Acknowledge' })).toBeEnabled();
  });

  it('says so when everything is in step', async () => {
    vi.mocked(dashboardApi.flaggedSync).mockResolvedValue([]);
    renderPage(<SyncConflictsPage />);
    expect(await screen.findByText(/No conflicts to review/)).toBeInTheDocument();
  });

  it('shows a retryable error instead of an empty list', async () => {
    vi.mocked(dashboardApi.flaggedSync).mockRejectedValue(new Error('boom'));
    renderPage(<SyncConflictsPage />);
    expect(await screen.findByText('Sync conflicts could not be loaded')).toBeInTheDocument();
    expect(screen.queryByText(/No conflicts to review/)).not.toBeInTheDocument();
  });
});
