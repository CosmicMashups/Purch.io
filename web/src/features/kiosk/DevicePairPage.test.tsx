import { fireEvent, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../lib/apiError';
import { useAuthStore } from '../../lib/authStore';
import { renderPage } from '../../test/render';
import { deviceApi } from './api';
import { DevicePairPage } from './DevicePairPage';
import { readDeviceCredential } from './deviceCredential';

vi.mock('./api', () => ({ deviceApi: { pair: vi.fn(), startSession: vi.fn() } }));

function b64(o: object) {
  return btoa(JSON.stringify(o)).replace(/=+$/, '').replace(/\+/g, '-').replace(/\//g, '_');
}
const kioskToken = `${b64({ alg: 'none' })}.${b64({ role: 'Kiosk', tenant_id: 't1' })}.x`;
const displayToken = `${b64({ alg: 'none' })}.${b64({ role: 'CustomerDisplay', tenant_id: 't1' })}.x`;

const paired = { deviceCredential: 'secret-1', deviceId: 'd1', tenantId: 't1', branchId: 'b1', deviceType: 1, name: 'Entrance kiosk' };
const kioskSession = { accessToken: kioskToken, refreshToken: 'r1', requiresStaff: false, deviceId: 'd1', deviceType: 1, name: 'Entrance kiosk' };

beforeEach(() => {
  vi.clearAllMocks();
  window.localStorage.clear();
  useAuthStore.setState({ accessToken: null, refreshToken: null });
});

function renderPair() {
  return renderPage(<DevicePairPage />, {
    route: '/pair',
    path: '/pair',
    otherRoutes: [
      { path: '/kiosk', element: <p>Kiosk home</p> },
      { path: '/order-board', element: <p>Board home</p> },
      { path: '/unlock', element: <p>Lock screen</p> },
      { path: '/customer-display', element: <p>Customer screen</p> },
    ],
  });
}

function enter(code: string) {
  fireEvent.change(screen.getByLabelText('Pairing code'), { target: { value: code } });
  fireEvent.click(screen.getByRole('button', { name: 'Pair this device' }));
}

describe('DevicePairPage', () => {
  it('asks for the code before calling the API', async () => {
    renderPair();
    fireEvent.click(screen.getByRole('button', { name: 'Pair this device' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Enter the pairing code');
    expect(deviceApi.pair).not.toHaveBeenCalled();
  });

  it('pairs with the code, keeps the credential, and opens the screen for that kind of device', async () => {
    vi.mocked(deviceApi.pair).mockResolvedValue(paired);
    vi.mocked(deviceApi.startSession).mockResolvedValue(kioskSession);
    renderPair();
    enter(' ab12cd34 ');
    expect(await screen.findByText('Kiosk home')).toBeInTheDocument();
    expect(deviceApi.pair).toHaveBeenCalledWith('ab12cd34');
    expect(deviceApi.startSession).toHaveBeenCalledWith('secret-1');
    expect(useAuthStore.getState().accessToken).toBe(kioskToken);
    expect(readDeviceCredential()).toBe('secret-1');
  });

  it('says a code that is wrong, used or expired was not recognised, without saying which', async () => {
    vi.mocked(deviceApi.pair).mockRejectedValue(new ApiError('unauthorized', 'raw'));
    renderPair();
    enter('ZZZZZZZZ');
    expect(await screen.findByRole('alert')).toHaveTextContent(/not recognised/i);
    expect(useAuthStore.getState().accessToken).toBeNull();
    expect(readDeviceCredential()).toBeNull();
  });

  it('hands a register or warehouse device to its lock screen, since a person operates it', async () => {
    vi.mocked(deviceApi.pair).mockResolvedValue({ ...paired, deviceType: 0, name: 'Front till' });
    vi.mocked(deviceApi.startSession).mockResolvedValue({ accessToken: null, refreshToken: null, requiresStaff: true, deviceId: 'd1', deviceType: 0, name: 'Front till' });
    renderPair();
    enter('AB12CD34');
    expect(await screen.findByText('Lock screen')).toBeInTheDocument();
    expect(useAuthStore.getState().accessToken).toBeNull();
    expect(readDeviceCredential()).toBe('secret-1');
  });

  it('opens the customer display screen for a customer display device', async () => {
    vi.mocked(deviceApi.pair).mockResolvedValue({ ...paired, deviceType: 5, name: 'Counter screen' });
    vi.mocked(deviceApi.startSession).mockResolvedValue({ accessToken: displayToken, refreshToken: 'r1', requiresStaff: false, deviceId: 'd1', deviceType: 5, name: 'Counter screen' });
    renderPair();
    enter('AB12CD34');
    expect(await screen.findByText('Customer screen')).toBeInTheDocument();
    expect(useAuthStore.getState().refreshToken).toBe('r1');
  });

  it('starts a session by itself when it was paired before and lost its sign-in', async () => {
    window.localStorage.setItem('purch.deviceCredential', 'secret-1');
    vi.mocked(deviceApi.startSession).mockResolvedValue(kioskSession);
    renderPair();
    expect(await screen.findByText('Kiosk home')).toBeInTheDocument();
    expect(deviceApi.startSession).toHaveBeenCalledWith('secret-1');
  });

  it('forgets a credential the server no longer accepts, such as a revoked device', async () => {
    window.localStorage.setItem('purch.deviceCredential', 'old');
    vi.mocked(deviceApi.startSession).mockRejectedValue(new ApiError('unauthorized', 'raw'));
    renderPair();
    await waitFor(() => expect(readDeviceCredential()).toBeNull());
    expect(screen.getByRole('button', { name: 'Pair this device' })).toBeInTheDocument();
  });

  it('skips pairing when this browser already is a device', async () => {
    useAuthStore.setState({ accessToken: kioskToken, refreshToken: 'r' });
    renderPair();
    await waitFor(() => expect(screen.getByText('Kiosk home')).toBeInTheDocument());
  });
});
