import { fireEvent, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../lib/apiError';
import { useAuthStore } from '../../lib/authStore';
import { renderPage } from '../../test/render';
import { readDeviceCredential } from '../kiosk/deviceCredential';
import { UnlockPage } from './UnlockPage';
import { unlockApi } from './unlockApi';

const roster = {
  deviceName: 'Front till',
  deviceType: 0,
  people: [
    { membershipId: 'm1', name: 'Ana Reyes', role: 0, hasPin: true },
    { membershipId: 'm2', name: 'Ben Santos', role: 2, hasPin: true },
  ],
};

beforeEach(() => {
  vi.restoreAllMocks();
  window.localStorage.clear();
  window.localStorage.setItem('purch.deviceCredential', 'secret-1');
  useAuthStore.setState({ accessToken: null, refreshToken: null });
});

function renderUnlock() {
  return renderPage(<UnlockPage />, {
    route: '/unlock',
    path: '/unlock',
    otherRoutes: [
      { path: '/sell', element: <p>Till</p> },
      { path: '/inventory', element: <p>Stock</p> },
      { path: '/', element: <p>Home</p> },
    ],
  });
}

function typePin(pin: string) {
  for (const digit of pin) fireEvent.click(screen.getByRole('button', { name: digit }));
}

describe('UnlockPage', () => {
  it('asks a device that was never paired to be paired', () => {
    window.localStorage.clear();
    renderUnlock();
    expect(screen.getByRole('heading', { name: 'This device is not set up' })).toBeInTheDocument();
  });

  it('lists the people who can work on this device and asks for the chosen person’s PIN', async () => {
    vi.spyOn(unlockApi, 'roster').mockResolvedValue(roster);
    renderUnlock();
    expect(await screen.findByRole('heading', { name: 'Front till is locked' })).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Ben Santos' }));
    expect(screen.getByText('Hello, Ben Santos. Type your PIN.')).toBeInTheDocument();
  });

  it('unlocks with the right PIN and opens the till for a staff member', async () => {
    vi.spyOn(unlockApi, 'roster').mockResolvedValue(roster);
    const unlock = vi.spyOn(unlockApi, 'unlock').mockResolvedValue({ accessToken: 'a1', refreshToken: 'r1', person: roster.people[1] });
    renderUnlock();
    fireEvent.click(await screen.findByRole('button', { name: 'Ben Santos' }));
    typePin('4821');
    fireEvent.click(screen.getByRole('button', { name: 'Unlock' }));

    expect(await screen.findByText('Till')).toBeInTheDocument();
    expect(unlock).toHaveBeenCalledWith('secret-1', 'm2', '4821');
    expect(useAuthStore.getState().accessToken).toBe('a1');
  });

  it('takes an Admin or Manager to the home page instead', async () => {
    vi.spyOn(unlockApi, 'roster').mockResolvedValue(roster);
    vi.spyOn(unlockApi, 'unlock').mockResolvedValue({ accessToken: 'a2', refreshToken: 'r2', person: roster.people[0] });
    renderUnlock();
    fireEvent.click(await screen.findByRole('button', { name: 'Ana Reyes' }));
    typePin('1234');
    fireEvent.click(screen.getByRole('button', { name: 'Unlock' }));
    expect(await screen.findByText('Home')).toBeInTheDocument();
  });

  it('opens Inventory on a warehouse device', async () => {
    vi.spyOn(unlockApi, 'roster').mockResolvedValue({ ...roster, deviceType: 4 });
    vi.spyOn(unlockApi, 'unlock').mockResolvedValue({ accessToken: 'a3', refreshToken: 'r3', person: roster.people[1] });
    renderUnlock();
    fireEvent.click(await screen.findByRole('button', { name: 'Ben Santos' }));
    typePin('4821');
    fireEvent.click(screen.getByRole('button', { name: 'Unlock' }));
    expect(await screen.findByText('Stock')).toBeInTheDocument();
  });

  it('shows how many tries are left, clears the PIN, and keeps the lock screen', async () => {
    vi.spyOn(unlockApi, 'roster').mockResolvedValue(roster);
    vi.spyOn(unlockApi, 'unlock').mockRejectedValue(new ApiError('unauthorized', 'That PIN is not right. 3 tries left.'));
    renderUnlock();
    fireEvent.click(await screen.findByRole('button', { name: 'Ben Santos' }));
    typePin('0000');
    fireEvent.click(screen.getByRole('button', { name: 'Unlock' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('3 tries left');
    expect(screen.getByLabelText('PIN entered')).toHaveTextContent('');
    expect(useAuthStore.getState().accessToken).toBeNull();
  });

  it('needs at least four digits before it will try, and lets the person back out', async () => {
    vi.spyOn(unlockApi, 'roster').mockResolvedValue(roster);
    const unlock = vi.spyOn(unlockApi, 'unlock');
    renderUnlock();
    fireEvent.click(await screen.findByRole('button', { name: 'Ben Santos' }));
    typePin('48');
    expect(screen.getByRole('button', { name: 'Unlock' })).toBeDisabled();
    fireEvent.click(screen.getByRole('button', { name: 'Not you? Choose someone else' }));
    expect(screen.getByRole('button', { name: 'Ana Reyes' })).toBeInTheDocument();
    expect(unlock).not.toHaveBeenCalled();
  });

  it('forgets a credential the server no longer accepts, such as a revoked device', async () => {
    vi.spyOn(unlockApi, 'roster').mockRejectedValue(new ApiError('unauthorized', 'raw'));
    renderUnlock();
    expect(await screen.findByRole('heading', { name: 'This device is not set up' })).toBeInTheDocument();
    await waitFor(() => expect(readDeviceCredential()).toBeNull());
  });
});
