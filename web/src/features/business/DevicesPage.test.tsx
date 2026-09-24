import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { useToastStore } from '../../components/feedback/toastStore';
import { renderPage, signInAs } from '../../test/render';
import { branchesApi } from '../branches/api';
import { deviceApi, type Device } from './deviceApi';
import { DevicesPage } from './DevicesPage';

vi.mock('./deviceApi', () => ({ deviceApi: { list: vi.fn(), create: vi.fn(), resetPairingCode: vi.fn(), resetPairingPin: vi.fn() } }));
vi.mock('../branches/api', () => ({ branchesApi: { list: vi.fn() } }));

const register: Device = { id: 'd1', branchId: 'kat', pairingCode: 'KAT-4821', deviceIdentifier: 'Front counter', deviceType: 0, lastSeenAt: '2026-09-24T02:00:00Z' };
const kiosk: Device = { id: 'd2', branchId: 'kat', pairingCode: 'KAT-9901', deviceIdentifier: null, deviceType: 1, lastSeenAt: null };

beforeEach(() => {
  vi.clearAllMocks();
  useToastStore.setState({ toasts: [] });
  signInAs('Admin');
  vi.mocked(deviceApi.list).mockResolvedValue([register, kiosk]);
  vi.mocked(branchesApi.list).mockResolvedValue([{ id: 'kat', name: 'Katipunan', address: null }]);
  vi.mocked(deviceApi.create).mockResolvedValue({ ...register, pairingCode: 'KAT-1111' });
});

describe('DevicesPage', () => {
  it('shows each pairing code large, with type, branch and last seen', async () => {
    renderPage(<DevicesPage />);
    expect(await screen.findByLabelText('Pairing code KAT-4821')).toBeInTheDocument();
    expect(screen.getByText('Register, Katipunan')).toBeInTheDocument();
    expect(screen.getByText('Never seen')).toBeInTheDocument();
    expect(screen.getByText(/^Last seen /)).toBeInTheDocument();
  });

  it('adds a register without a PIN', async () => {
    renderPage(<DevicesPage />);
    fireEvent.change(await screen.findByLabelText('Name (optional)'), { target: { value: 'Back office' } });
    fireEvent.click(screen.getByRole('button', { name: 'Add' }));
    await waitFor(() => expect(deviceApi.create).toHaveBeenCalledWith({ branchId: 'kat', deviceIdentifier: 'Back office', deviceType: 0, pairingPin: null }));
    await waitFor(() => expect(useToastStore.getState().toasts[0]?.message).toBe('Device added. Pairing code: KAT-1111'));
  });

  it('insists on a PIN for a kiosk and sends it when given', async () => {
    renderPage(<DevicesPage />);
    fireEvent.change(await screen.findByLabelText('Type'), { target: { value: '1' } });
    fireEvent.click(screen.getByRole('button', { name: 'Add' }));
    expect(await screen.findByText('A pairing PIN is required for this device type')).toBeInTheDocument();
    expect(deviceApi.create).not.toHaveBeenCalled();

    fireEvent.change(screen.getByLabelText('Pairing PIN'), { target: { value: '55aa' } });
    fireEvent.click(screen.getByRole('button', { name: 'Add' }));
    expect(await screen.findByText('Use 4 to 8 digits')).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText('Pairing PIN'), { target: { value: '5566' } });
    fireEvent.click(screen.getByRole('button', { name: 'Add' }));
    await waitFor(() => expect(deviceApi.create).toHaveBeenCalledWith({ branchId: 'kat', deviceIdentifier: null, deviceType: 1, pairingPin: '5566' }));
  });

  it('warns and confirms before making a new pairing code, then shows the new code', async () => {
    vi.mocked(deviceApi.resetPairingCode).mockResolvedValue({ ...register, pairingCode: 'KAT-7777' });
    renderPage(<DevicesPage />);
    fireEvent.click((await screen.findAllByRole('button', { name: 'New pairing code' }))[0]);
    const dialog = screen.getByRole('dialog', { name: 'Make a new pairing code?' });
    expect(within(dialog).getByText(/signed out/)).toBeInTheDocument();
    expect(deviceApi.resetPairingCode).not.toHaveBeenCalled();

    fireEvent.click(within(dialog).getByRole('button', { name: 'Make new code' }));
    await waitFor(() => expect(deviceApi.resetPairingCode).toHaveBeenCalledWith('d1'));
    await waitFor(() => expect(useToastStore.getState().toasts[0]?.message).toBe('New pairing code: KAT-7777'));
  });

  it('backing out of a new pairing code changes nothing', async () => {
    renderPage(<DevicesPage />);
    fireEvent.click((await screen.findAllByRole('button', { name: 'New pairing code' }))[0]);
    fireEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Cancel' }));
    expect(deviceApi.resetPairingCode).not.toHaveBeenCalled();
  });

  it('changes a device PIN with the 4 to 8 digit rule', async () => {
    vi.mocked(deviceApi.resetPairingPin).mockResolvedValue(kiosk);
    renderPage(<DevicesPage />);
    fireEvent.click((await screen.findAllByRole('button', { name: 'Change PIN' }))[1]);
    const dialog = screen.getByRole('dialog', { name: 'Change device PIN' });

    fireEvent.click(within(dialog).getByRole('button', { name: 'Change PIN' }));
    expect(await within(dialog).findByText('A pairing PIN is required for this device type')).toBeInTheDocument();
    expect(deviceApi.resetPairingPin).not.toHaveBeenCalled();

    fireEvent.change(within(dialog).getByLabelText('New PIN'), { target: { value: '7788' } });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Change PIN' }));
    await waitFor(() => expect(deviceApi.resetPairingPin).toHaveBeenCalledWith('d2', '7788'));
  });

  it('shows a retryable error instead of an empty list', async () => {
    vi.mocked(deviceApi.list).mockRejectedValue(new Error('boom'));
    renderPage(<DevicesPage />);
    expect(await screen.findByText('Devices could not be loaded')).toBeInTheDocument();
  });
});
