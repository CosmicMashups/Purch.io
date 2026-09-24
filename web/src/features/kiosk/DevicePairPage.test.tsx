import { fireEvent, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../lib/apiError';
import { useAuthStore } from '../../lib/authStore';
import { renderPage } from '../../test/render';
import { deviceApi } from './api';
import { DevicePairPage } from './DevicePairPage';

vi.mock('./api', () => ({ deviceApi: { pair: vi.fn() } }));

function b64(o: object) {
  return btoa(JSON.stringify(o)).replace(/=+$/, '').replace(/\+/g, '-').replace(/\//g, '_');
}
const kioskToken = `${b64({ alg: 'none' })}.${b64({ role: 'Kiosk', tenant_id: 't1' })}.x`;

beforeEach(() => {
  vi.clearAllMocks();
  useAuthStore.setState({ accessToken: null, refreshToken: null });
});

function renderPair(role: 'Kiosk' | 'OrderBoard' = 'Kiosk') {
  return renderPage(<DevicePairPage role={role} />, {
    route: '/pair',
    path: '/pair',
    otherRoutes: [
      { path: '/kiosk', element: <p>Kiosk home</p> },
      { path: '/order-board', element: <p>Board home</p> },
    ],
  });
}

function fill(code: string, pin: string) {
  fireEvent.change(screen.getByLabelText('Device code'), { target: { value: code } });
  fireEvent.change(screen.getByLabelText('PIN'), { target: { value: pin } });
  fireEvent.click(screen.getByRole('button', { name: 'Pair this device' }));
}

describe('DevicePairPage', () => {
  it('asks for both fields before calling the API', async () => {
    renderPair();
    fireEvent.click(screen.getByRole('button', { name: 'Pair this device' }));
    expect(await screen.findByText('Enter the device code')).toBeInTheDocument();
    expect(screen.getByText('Enter the PIN')).toBeInTheDocument();
    expect(deviceApi.pair).not.toHaveBeenCalled();
  });

  it('stores the session and opens the screen for that device', async () => {
    vi.mocked(deviceApi.pair).mockResolvedValue({ accessToken: kioskToken, refreshToken: 'r1' });
    renderPair();
    fill(' K-100 ', '1234');
    expect(await screen.findByText('Kiosk home')).toBeInTheDocument();
    expect(deviceApi.pair).toHaveBeenCalledWith('Kiosk', 'K-100', '1234');
    expect(useAuthStore.getState().accessToken).toBe(kioskToken);
  });

  it('says the code was not recognised without saying which part was wrong', async () => {
    vi.mocked(deviceApi.pair).mockRejectedValue(new ApiError('unauthorized', 'raw'));
    renderPair('OrderBoard');
    fill('B-1', '0000');
    expect(await screen.findByRole('alert')).toHaveTextContent(/not recognised/i);
    expect(useAuthStore.getState().accessToken).toBeNull();
  });

  it('skips pairing when this browser already is that device', async () => {
    useAuthStore.setState({ accessToken: kioskToken, refreshToken: 'r' });
    renderPair();
    await waitFor(() => expect(screen.getByText('Kiosk home')).toBeInTheDocument());
  });
});
