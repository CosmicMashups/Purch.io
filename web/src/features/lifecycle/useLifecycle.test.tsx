import { act, fireEvent, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { Toaster } from '../../components/feedback/Toaster';
import { useToastStore } from '../../components/feedback/toastStore';
import { renderPage } from '../../test/render';
import { lifecycleApi, type LifecycleImpact } from './api';
import { useLifecycle } from './useLifecycle';

vi.mock('./api', () => ({ lifecycleApi: { impact: vi.fn(), act: vi.fn(), deleted: vi.fn() } }));

function Harness() {
  const { run, dialog } = useLifecycle();
  return (
    <>
      <button type="button" onClick={() => run({ kind: 'Category', id: 'c1', name: 'Drinks' }, 'deactivate')}>
        Make Drinks inactive
      </button>
      <button type="button" onClick={() => run({ kind: 'Category', id: 'c1', name: 'Drinks' }, 'delete')}>
        Delete Drinks
      </button>
      <button type="button" onClick={() => run({ kind: 'Category', id: 'c1', name: 'Drinks' }, 'restore')}>
        Restore Drinks
      </button>
      {dialog}
      <Toaster />
    </>
  );
}

const impact = (over: Partial<LifecycleImpact> = {}): LifecycleImpact => ({
  id: 'c1',
  name: 'Drinks',
  status: 'Active',
  notes: ['Used by 12 items.'],
  deactivateBlockedReason: null,
  deleteBlockedReason: null,
  ...over,
});

beforeEach(() => {
  vi.clearAllMocks();
  useToastStore.setState({ toasts: [] });
  vi.mocked(lifecycleApi.impact).mockResolvedValue(impact());
  vi.mocked(lifecycleApi.act).mockResolvedValue({ id: 'c1', name: 'Drinks', status: 'Inactive' });
});
afterEach(() => vi.useRealTimers());

describe('useLifecycle', () => {
  it('asks first, naming what depends on the record, and changes nothing until confirmed', async () => {
    renderPage(<Harness />);
    fireEvent.click(screen.getByRole('button', { name: 'Make Drinks inactive' }));

    const dialog = await screen.findByRole('dialog', { name: 'Make Drinks inactive?' });
    expect(await within(dialog).findByText('Used by 12 items.')).toBeInTheDocument();
    expect(lifecycleApi.act).not.toHaveBeenCalled();

    fireEvent.click(within(dialog).getByRole('button', { name: 'Cancel' }));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(lifecycleApi.act).not.toHaveBeenCalled();
  });

  it('after confirming, shows a five second countdown with Undo, and Undo puts it back', async () => {
    renderPage(<Harness />);
    fireEvent.click(screen.getByRole('button', { name: 'Make Drinks inactive' }));
    const dialog = await screen.findByRole('dialog');
    await within(dialog).findByText('Used by 12 items.');
    fireEvent.click(within(dialog).getByRole('button', { name: 'Make inactive' }));

    await waitFor(() => expect(lifecycleApi.act).toHaveBeenCalledWith('Category', 'c1', 'deactivate'));
    const undo = await screen.findByRole('button', { name: /Undo/ });
    expect(undo).toHaveTextContent('(5)');
    expect(screen.getByText('Drinks is now inactive')).toBeInTheDocument();

    fireEvent.click(undo);
    await waitFor(() => expect(lifecycleApi.act).toHaveBeenLastCalledWith('Category', 'c1', 'reactivate'));
  });

  it('the snackbar goes away after five seconds if Undo is not pressed', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    renderPage(<Harness />);
    fireEvent.click(screen.getByRole('button', { name: 'Make Drinks inactive' }));
    const dialog = await screen.findByRole('dialog');
    await within(dialog).findByText('Used by 12 items.');
    fireEvent.click(within(dialog).getByRole('button', { name: 'Make inactive' }));
    await screen.findByRole('button', { name: /Undo/ });

    act(() => {
      vi.advanceTimersByTime(5100);
    });
    expect(screen.queryByRole('button', { name: /Undo/ })).not.toBeInTheDocument();
    expect(lifecycleApi.act).toHaveBeenCalledTimes(1);
  });

  it('deleting asks first and says the record can be restored; undo restores it live if it was live', async () => {
    vi.mocked(lifecycleApi.act).mockResolvedValue({ id: 'c1', name: 'Drinks', status: 'Deleted' });
    renderPage(<Harness />);
    fireEvent.click(screen.getByRole('button', { name: 'Delete Drinks' }));
    const dialog = await screen.findByRole('dialog', { name: 'Delete Drinks?' });
    expect(within(dialog).getByText(/You can restore it later/)).toBeInTheDocument();
    await within(dialog).findByText('Used by 12 items.');
    fireEvent.click(within(dialog).getByRole('button', { name: 'Delete' }));

    await waitFor(() => expect(lifecycleApi.act).toHaveBeenCalledWith('Category', 'c1', 'delete'));
    fireEvent.click(await screen.findByRole('button', { name: /Undo/ }));
    await waitFor(() => expect(lifecycleApi.act).toHaveBeenCalledWith('Category', 'c1', 'restore'));
    // It was Active before the delete, so undo also switches it back on rather than leaving it inactive.
    await waitFor(() => expect(lifecycleApi.act).toHaveBeenLastCalledWith('Category', 'c1', 'reactivate'));
  });

  it('shows why a change is refused and offers no way to confirm it', async () => {
    vi.mocked(lifecycleApi.impact).mockResolvedValue(impact({ notes: [], deleteBlockedReason: 'A shift is open at this branch. Close it first.' }));
    renderPage(<Harness />);
    fireEvent.click(screen.getByRole('button', { name: 'Delete Drinks' }));
    const dialog = await screen.findByRole('dialog');
    expect(await within(dialog).findByRole('alert')).toHaveTextContent('A shift is open at this branch');
    expect(within(dialog).queryByRole('button', { name: 'Delete' })).not.toBeInTheDocument();
    expect(lifecycleApi.act).not.toHaveBeenCalled();
  });

  it('restoring needs no confirmation and says it comes back inactive', async () => {
    vi.mocked(lifecycleApi.act).mockResolvedValue({ id: 'c1', name: 'Drinks', status: 'Inactive' });
    renderPage(<Harness />);
    fireEvent.click(screen.getByRole('button', { name: 'Restore Drinks' }));
    await waitFor(() => expect(lifecycleApi.act).toHaveBeenCalledWith('Category', 'c1', 'restore'));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(await screen.findByText(/restored as an inactive category/)).toBeInTheDocument();
  });
});
