import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { renderPage, signInAs } from '../../test/render';
import { RequireRole } from './RequireRole';

function renderGuarded() {
  return renderPage(<RequireRole allow={['Admin']} />, {
    route: '/secret',
    path: '/secret',
    otherRoutes: [{ path: '/business', element: <p>Business home</p> }],
  });
}

describe('RequireRole', () => {
  it('sends other roles back to Business', () => {
    signInAs('Manager');
    renderGuarded();
    expect(screen.getByText('Business home')).toBeInTheDocument();
  });

  it('sends a device role or an unreadable token back too', () => {
    signInAs('Kiosk');
    renderGuarded();
    expect(screen.getByText('Business home')).toBeInTheDocument();
  });

});
