import { act, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { useAuthStore } from '../../lib/authStore';
import { renderPage } from '../../test/render';
import * as serverFeed from './serverFeed';
import { IDLE_STATE, type CustomerDisplayState } from './channel';
import { CustomerDisplayPage } from './CustomerDisplayPage';

let push: (s: CustomerDisplayState) => void = () => undefined;
let supported = true;

vi.mock('./channel', async (importOriginal) => {
  const actual = await importOriginal<typeof import('./channel')>();
  return {
    ...actual,
    customerDisplaySupported: () => supported,
    subscribeCustomerDisplay: (cb: (s: CustomerDisplayState) => void) => {
      push = cb;
      return () => undefined;
    },
  };
});

beforeEach(() => {
  supported = true;
  useAuthStore.setState({ accessToken: null, refreshToken: null });
});

function b64(o: object) {
  return btoa(JSON.stringify(o)).replace(/=+$/, '').replace(/\+/g, '-').replace(/\//g, '_');
}

describe('CustomerDisplayPage', () => {
  it('welcomes the customer before anything is rung up', () => {
    renderPage(<CustomerDisplayPage />);
    expect(screen.getByRole('heading', { name: 'Welcome!' })).toBeInTheDocument();
  });

  it('follows the order as it is built', () => {
    renderPage(<CustomerDisplayPage />);
    act(() =>
      push({ ...IDLE_STATE, mode: 'cart', lines: [{ name: 'Latte', quantity: 2, unitPrice: 150, lineTotal: 300 }], savings: [{ label: 'Item promotions', amount: 20 }], subtotal: 300, total: 280 }),
    );
    expect(screen.getByText('Latte')).toBeInTheDocument();
    expect(screen.getByText('₱280.00')).toBeInTheDocument();
    expect(screen.getByText('Item promotions')).toBeInTheDocument();
  });

  it('says how much to pay at the payment step', () => {
    renderPage(<CustomerDisplayPage />);
    act(() => push({ ...IDLE_STATE, mode: 'payment', lines: [{ name: 'Tea', quantity: 1, unitPrice: 90, lineTotal: 90 }], subtotal: 90, total: 90 }));
    expect(screen.getByRole('heading', { name: 'Amount to pay' })).toBeInTheDocument();
  });

  it('thanks the customer and shows their change', () => {
    renderPage(<CustomerDisplayPage />);
    act(() => push({ ...IDLE_STATE, mode: 'completed', change: 30 }));
    expect(screen.getByRole('heading', { name: 'Thank you!' })).toBeInTheDocument();
    expect(screen.getByText('₱30.00')).toBeInTheDocument();
  });

  it('explains itself when the browser cannot share between windows', () => {
    supported = false;
    renderPage(<CustomerDisplayPage />);
    expect(screen.getByText(/cannot share the order between windows/i)).toBeInTheDocument();
  });
  it('follows its Register through the server when it is paired as a customer display', async () => {
    useAuthStore.setState({ accessToken: `${b64({ alg: 'none' })}.${b64({ role: 'CustomerDisplay', tenant_id: 't1', device_id: 'd1' })}.x`, refreshToken: 'r' });
    vi.spyOn(serverFeed.customerDisplayFeed, 'poll').mockResolvedValue({
      version: 4,
      updatedAt: null,
      state: { ...IDLE_STATE, mode: 'cart', lines: [{ name: 'Tea', quantity: 1, unitPrice: 90, lineTotal: 90 }], subtotal: 90, total: 90, vat: 9.64 },
    });
    renderPage(<CustomerDisplayPage />);
    expect(await screen.findByText('Tea')).toBeInTheDocument();
    vi.restoreAllMocks();
  });

  it('works without the browser channel when it is a paired display', async () => {
    supported = false;
    useAuthStore.setState({ accessToken: `${b64({ alg: 'none' })}.${b64({ role: 'CustomerDisplay', tenant_id: 't1', device_id: 'd1' })}.x`, refreshToken: 'r' });
    vi.spyOn(serverFeed.customerDisplayFeed, 'poll').mockResolvedValue({ version: 0, updatedAt: null, state: null });
    renderPage(<CustomerDisplayPage />);
    expect(await screen.findByRole('heading', { name: 'Welcome!' })).toBeInTheDocument();
    vi.restoreAllMocks();
  });
});
