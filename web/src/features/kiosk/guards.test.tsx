import { screen } from '@testing-library/react';
import { beforeEach, describe, expect, it } from 'vitest';
import { useAuthStore } from '../../lib/authStore';
import { renderPage, signInAs } from '../../test/render';
import { RequireAuth } from '../auth/RequireAuth';
import { RequireDevice } from './RequireDevice';

beforeEach(() => useAuthStore.setState({ accessToken: null, refreshToken: null }));

function guardedDevice(role: 'Kiosk' | 'KitchenDisplay') {
  return renderPage(<RequireDevice role={role} />, {
    route: '/screen',
    path: '/screen',
    otherRoutes: [
      { path: '/kiosk/pair', element: <p>Kiosk pairing</p> },
      { path: '/kitchen/pair', element: <p>Kitchen pairing</p> },
    ],
  });
}

describe('RequireDevice', () => {
  it('sends an unpaired browser to that device pairing screen', () => {
    guardedDevice('Kiosk');
    expect(screen.getByText('Kiosk pairing')).toBeInTheDocument();
  });

  it('never shows a screen to a browser paired as something else, and says so', () => {
    signInAs('Cashier');
    guardedDevice('Kiosk');
    expect(screen.getByRole('heading', { name: 'This browser is set up for something else' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Sign out' })).toBeInTheDocument();
  });

  it('keeps a kitchen display out of the kiosk', () => {
    signInAs('KitchenDisplay');
    guardedDevice('Kiosk');
    expect(screen.getByText(/signed in as a kitchen display/i)).toBeInTheDocument();
  });
});

describe('RequireAuth with a device token', () => {
  const staff = () =>
    renderPage(<RequireAuth />, {
      route: '/',
      path: '/',
      otherRoutes: [
        { path: '/kiosk', element: <p>Kiosk home</p> },
        { path: '/kitchen', element: <p>Kitchen home</p> },
        { path: '/order-board', element: <p>Board home</p> },
        { path: '/login', element: <p>Login</p> },
      ],
    });

  it.each([
    ['Kiosk', 'Kiosk home'],
    ['KitchenDisplay', 'Kitchen home'],
    ['OrderBoard', 'Board home'],
  ])('keeps a %s token out of the staff shell', (role, text) => {
    signInAs(role);
    staff();
    expect(screen.getByText(text)).toBeInTheDocument();
  });

  it('still sends nobody to login', () => {
    staff();
    expect(screen.getByText('Login')).toBeInTheDocument();
  });
});
