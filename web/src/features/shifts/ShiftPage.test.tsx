import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { useToastStore } from '../../components/feedback/toastStore';
import { ApiError } from '../../lib/apiError';
import { renderPage, signInAs } from '../../test/render';
import { shiftsApi } from './api';
import { ShiftPage } from './ShiftPage';
import { ShiftStatus, type Shift } from './types';

vi.mock('./api', () => ({ shiftsApi: { current: vi.fn(), open: vi.fn(), close: vi.fn() } }));

const openShift: Shift = {
  id: 's1',
  branchId: 'kat',
  deviceId: 'dev1',
  status: ShiftStatus.Open,
  openedByUserId: 'u1',
  openedByUserName: 'Ana Reyes',
  openingCashAmount: 1500,
  openedAt: '2026-09-24T01:00:00Z',
  closedByUserId: null,
  closedByUserName: null,
  closingCashAmount: null,
  expectedCashAmount: null,
  varianceAmount: null,
  handoverNotes: null,
  approvedByUserId: null,
  approvedByUserName: null,
  closedAt: null,
};

const closedShort: Shift = {
  ...openShift,
  status: ShiftStatus.Closed,
  closedByUserName: 'Ana Reyes',
  closingCashAmount: 4200,
  expectedCashAmount: 4240,
  varianceAmount: -40,
  approvedByUserName: 'Mario Cruz',
  handoverNotes: 'Coins short',
  closedAt: '2026-09-24T10:00:00Z',
};

beforeEach(() => {
  vi.clearAllMocks();
  useToastStore.setState({ toasts: [] });
  signInAs('Cashier', { device_id: 'dev1', branch_id: 'kat' });
});

describe('ShiftPage access', () => {
  it('needs a paired device', () => {
    signInAs('Admin');
    renderPage(<ShiftPage />);
    expect(screen.getByText('Sign in with a device to sell')).toBeInTheDocument();
    expect(shiftsApi.current).not.toHaveBeenCalled();
  });
});

describe('opening a shift', () => {
  beforeEach(() => {
    vi.mocked(shiftsApi.current).mockResolvedValue(null);
  });

  it('refuses a blank or negative opening count without calling the API', async () => {
    renderPage(<ShiftPage />);
    fireEvent.click(await screen.findByRole('button', { name: 'Open shift' }));
    expect(await screen.findByText('Enter the cash amount')).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText('Opening cash count (PHP)'), { target: { value: '-5' } });
    fireEvent.click(screen.getByRole('button', { name: 'Open shift' }));
    expect(await screen.findByText('Cannot be negative')).toBeInTheDocument();
    expect(shiftsApi.open).not.toHaveBeenCalled();
  });

  it('opens with the counted amount and switches to the open-shift view', async () => {
    vi.mocked(shiftsApi.open).mockResolvedValue(openShift);
    renderPage(<ShiftPage />);
    fireEvent.change(await screen.findByLabelText('Opening cash count (PHP)'), { target: { value: '1500' } });
    fireEvent.click(screen.getByRole('button', { name: 'Open shift' }));
    await waitFor(() => expect(shiftsApi.open).toHaveBeenCalledWith(1500));
    expect(await screen.findByText('Shift open')).toBeInTheDocument();
    expect(screen.getByText('Ana Reyes')).toBeInTheDocument();
  });

  it('accepts a zero opening count', async () => {
    vi.mocked(shiftsApi.open).mockResolvedValue(openShift);
    renderPage(<ShiftPage />);
    fireEvent.change(await screen.findByLabelText('Opening cash count (PHP)'), { target: { value: '0' } });
    fireEvent.click(screen.getByRole('button', { name: 'Open shift' }));
    await waitFor(() => expect(shiftsApi.open).toHaveBeenCalledWith(0));
  });
});

describe('closing a shift', () => {
  beforeEach(() => {
    vi.mocked(shiftsApi.current).mockResolvedValue(openShift);
  });

  it('shows who opened it and the opening cash', async () => {
    renderPage(<ShiftPage />);
    expect(await screen.findByText('Ana Reyes')).toBeInTheDocument();
    expect(screen.getByText('₱1,500.00')).toBeInTheDocument();
  });

  it('asks before closing and sends nothing if the cashier backs out', async () => {
    renderPage(<ShiftPage />);
    fireEvent.change(await screen.findByLabelText(/Closing cash count/), { target: { value: '4200' } });
    fireEvent.click(screen.getByRole('button', { name: 'Close shift' }));

    const dialog = screen.getByRole('dialog', { name: 'Close this shift?' });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Cancel' }));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(shiftsApi.close).not.toHaveBeenCalled();
  });

  it('will not open the confirm dialog without a count', async () => {
    renderPage(<ShiftPage />);
    fireEvent.click(await screen.findByRole('button', { name: 'Close shift' }));
    expect(await screen.findByText('Enter the cash amount')).toBeInTheDocument();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('closes, sending blank notes and PIN as null, and shows the server reconciliation', async () => {
    vi.mocked(shiftsApi.close).mockResolvedValue({ ...closedShort, approvedByUserName: null, handoverNotes: null, varianceAmount: 0, expectedCashAmount: 4200 });
    renderPage(<ShiftPage />);
    fireEvent.change(await screen.findByLabelText(/Closing cash count/), { target: { value: '4200' } });
    fireEvent.click(screen.getByRole('button', { name: 'Close shift' }));
    fireEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Close shift' }));

    await waitFor(() => expect(shiftsApi.close).toHaveBeenCalledWith({ closingCashAmount: 4200, handoverNotes: null, approverPin: null }));
    expect(await screen.findByText('Matched exactly')).toBeInTheDocument();
  });

  it('sends the manager PIN and notes, then reports a shortage with who approved it', async () => {
    vi.mocked(shiftsApi.close).mockResolvedValue(closedShort);
    renderPage(<ShiftPage />);
    fireEvent.change(await screen.findByLabelText(/Closing cash count/), { target: { value: '4200' } });
    fireEvent.change(screen.getByLabelText('Handover notes (optional)'), { target: { value: ' Coins short ' } });
    fireEvent.change(screen.getByLabelText(/Manager or admin PIN/), { target: { value: '4321' } });
    fireEvent.click(screen.getByRole('button', { name: 'Close shift' }));
    fireEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Close shift' }));

    await waitFor(() => expect(shiftsApi.close).toHaveBeenCalledWith({ closingCashAmount: 4200, handoverNotes: 'Coins short', approverPin: '4321' }));
    expect(await screen.findByText('Short by ₱40.00')).toBeInTheDocument();
    expect(screen.getByText('Approved by Mario Cruz')).toBeInTheDocument();
    expect(screen.getByText('₱4,240.00')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Done' })).toHaveAttribute('href', '/sell');
  });

  it('keeps the form open and explains when the server needs a manager PIN', async () => {
    vi.mocked(shiftsApi.close).mockRejectedValue(new ApiError('validation', 'The count is off, so a manager or admin PIN is required.'));
    renderPage(<ShiftPage />);
    fireEvent.change(await screen.findByLabelText(/Closing cash count/), { target: { value: '100' } });
    fireEvent.click(screen.getByRole('button', { name: 'Close shift' }));
    fireEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Close shift' }));

    await waitFor(() => expect(useToastStore.getState().toasts[0]?.message).toBe('The count is off, so a manager or admin PIN is required.'));
    expect(screen.queryByText('Shift closed')).not.toBeInTheDocument();
    expect(screen.getByLabelText(/Closing cash count/)).toHaveValue('100');
  });

  it('shows a retryable error when the shift cannot load', async () => {
    vi.mocked(shiftsApi.current).mockRejectedValue(new Error('boom'));
    renderPage(<ShiftPage />);
    expect(await screen.findByText('The shift could not be loaded')).toBeInTheDocument();
  });
});
