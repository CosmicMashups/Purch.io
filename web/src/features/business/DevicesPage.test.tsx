import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderPage, signInAs } from '../../test/render';
import { branchesApi } from '../branches/api';
import { deviceApi, type Device } from './deviceApi';
import { DevicesPage } from './DevicesPage';

vi.mock('./deviceApi', () => ({ deviceApi: { list: vi.fn(), createPairing: vi.fn(), newPairingCode: vi.fn(), revoke: vi.fn() } }));
vi.mock('../branches/api', () => ({ branchesApi: { list: vi.fn() } }));

const register: Device = { id: 'd1', branchId: 'kat', pairingCode: '', deviceIdentifier: null, name: 'Front counter', deviceType: 0, status: 0, lastSeenAt: '2026-09-24T02:00:00Z' };
const kiosk: Device = { id: 'd2', branchId: 'kat', pairingCode: '', deviceIdentifier: null, name: 'Entrance kiosk', deviceType: 1, status: 1, lastSeenAt: null, pairingCodeExpiresAt: '2026-10-03T03:00:00Z' };
const retired: Device = { id: 'd3', branchId: 'kat', pairingCode: '', deviceIdentifier: null, name: 'Old tablet', deviceType: 2, status: 2, lastSeenAt: null };

const issued = (device: Device, code: string) => ({ device, pairingCode: code, expiresAt: '2026-10-03T03:10:00Z' });

beforeEach(() => {
  vi.clearAllMocks();
  signInAs('Admin');
  vi.mocked(deviceApi.list).mockResolvedValue([register, kiosk, retired]);
  vi.mocked(branchesApi.list).mockResolvedValue([{ id: 'kat', name: 'Katipunan', address: null }]);
});

describe('DevicesPage', () => {
  it('shows each device with its name, type, branch and where it is in pairing', async () => {
    renderPage(<DevicesPage />);
    expect(await screen.findByText('Front counter')).toBeInTheDocument();
    expect(screen.getByText('Paired')).toBeInTheDocument();
    expect(screen.getByText('Waiting for its code')).toBeInTheDocument();
    expect(screen.getByText('Revoked')).toBeInTheDocument();
    expect(screen.getByText('Register, Katipunan')).toBeInTheDocument();
    expect(screen.getAllByText('Never seen').length).toBeGreaterThan(0);
  });

  it('adds a device and shows its one-time code once', async () => {
    vi.mocked(deviceApi.createPairing).mockResolvedValue(issued({ ...kiosk, id: 'd9', name: 'Back till', deviceType: 0 }, 'K4RT7WQ2'));
    renderPage(<DevicesPage />);
    fireEvent.change(await screen.findByLabelText('Name'), { target: { value: ' Back till ' } });
    fireEvent.click(screen.getByRole('button', { name: 'Add' }));

    await waitFor(() => expect(deviceApi.createPairing).toHaveBeenCalledWith({ branchId: 'kat', name: 'Back till', deviceType: 0, linkedRegisterDeviceId: null }));
    const dialog = await screen.findByRole('dialog', { name: 'Enter this code on the device' });
    expect(within(dialog).getByLabelText('Pairing code K4RT7WQ2')).toBeInTheDocument();
    expect(within(dialog).getByText(/works once/)).toBeInTheDocument();
  });

  it('needs a name, and a customer display needs the Register it shows', async () => {
    renderPage(<DevicesPage />);
    fireEvent.click(await screen.findByRole('button', { name: 'Add' }));
    expect(await screen.findByText('Give the device a name')).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText('Name'), { target: { value: 'Counter screen' } });
    fireEvent.change(screen.getByLabelText('Type'), { target: { value: '5' } });
    fireEvent.click(screen.getByRole('button', { name: 'Add' }));
    expect(await screen.findByText('Choose the Register this screen shows')).toBeInTheDocument();
    expect(deviceApi.createPairing).not.toHaveBeenCalled();

    vi.mocked(deviceApi.createPairing).mockResolvedValue(issued({ ...register, id: 'd8', name: 'Counter screen', deviceType: 5 }, 'SCREEN22'));
    fireEvent.change(screen.getByLabelText('Shows which Register'), { target: { value: 'd1' } });
    fireEvent.click(screen.getByRole('button', { name: 'Add' }));
    await waitFor(() => expect(deviceApi.createPairing).toHaveBeenCalledWith({ branchId: 'kat', name: 'Counter screen', deviceType: 5, linkedRegisterDeviceId: 'd1' }));
  });

  it('warns before pairing an active device again, then shows the new code', async () => {
    vi.mocked(deviceApi.newPairingCode).mockResolvedValue(issued({ ...register, status: 1 }, 'NEWCODE9'));
    renderPage(<DevicesPage />);
    fireEvent.click((await screen.findAllByRole('button', { name: 'New pairing code' }))[0]);
    const confirm = screen.getByRole('dialog', { name: 'Make a new pairing code?' });
    expect(within(confirm).getByText(/signed out/)).toBeInTheDocument();
    expect(deviceApi.newPairingCode).not.toHaveBeenCalled();

    fireEvent.click(within(confirm).getByRole('button', { name: 'Make new code' }));
    await waitFor(() => expect(deviceApi.newPairingCode).toHaveBeenCalledWith('d1'));
    expect(await screen.findByLabelText('Pairing code NEWCODE9')).toBeInTheDocument();
  });

  it('gives a device that is still waiting a fresh code without a warning', async () => {
    vi.mocked(deviceApi.newPairingCode).mockResolvedValue(issued(kiosk, 'FRESH222'));
    renderPage(<DevicesPage />);
    fireEvent.click((await screen.findAllByRole('button', { name: 'New pairing code' }))[1]);
    await waitFor(() => expect(deviceApi.newPairingCode).toHaveBeenCalledWith('d2'));
    expect(await screen.findByLabelText('Pairing code FRESH222')).toBeInTheDocument();
  });

  it('asks before revoking, and backing out changes nothing', async () => {
    vi.mocked(deviceApi.revoke).mockResolvedValue({ ...register, status: 2 });
    renderPage(<DevicesPage />);
    fireEvent.click((await screen.findAllByRole('button', { name: 'Revoke' }))[0]);
    fireEvent.click(within(screen.getByRole('dialog', { name: 'Revoke this device?' })).getByRole('button', { name: 'Cancel' }));
    expect(deviceApi.revoke).not.toHaveBeenCalled();

    fireEvent.click(screen.getAllByRole('button', { name: 'Revoke' })[0]);
    fireEvent.click(within(screen.getByRole('dialog', { name: 'Revoke this device?' })).getByRole('button', { name: 'Revoke' }));
    await waitFor(() => expect(deviceApi.revoke).toHaveBeenCalledWith('d1'));
  });

  it('does not offer to revoke a device that is already revoked', async () => {
    renderPage(<DevicesPage />);
    await screen.findByText('Old tablet');
    expect(screen.getAllByRole('button', { name: 'Revoke' })).toHaveLength(2);
  });

  it('shows a retryable error instead of an empty list', async () => {
    vi.mocked(deviceApi.list).mockRejectedValue(new Error('boom'));
    renderPage(<DevicesPage />);
    expect(await screen.findByText('Devices could not be loaded')).toBeInTheDocument();
  });
});
