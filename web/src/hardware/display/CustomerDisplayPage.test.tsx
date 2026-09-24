import { act, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderPage } from '../../test/render';
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
});

describe('CustomerDisplayPage', () => {
  it('welcomes the customer before anything is rung up', () => {
    renderPage(<CustomerDisplayPage />);
    expect(screen.getByRole('heading', { name: 'Welcome!' })).toBeInTheDocument();
  });

  it('follows the order as it is built', () => {
    renderPage(<CustomerDisplayPage />);
    act(() =>
      push({ ...IDLE_STATE, mode: 'cart', lines: [{ name: 'Latte', quantity: 2, lineTotal: 300 }], savings: [{ label: 'Item promotions', amount: 20 }], subtotal: 300, total: 280 }),
    );
    expect(screen.getByText('Latte')).toBeInTheDocument();
    expect(screen.getByText('₱280.00')).toBeInTheDocument();
    expect(screen.getByText('Item promotions')).toBeInTheDocument();
  });

  it('says how much to pay at the payment step', () => {
    renderPage(<CustomerDisplayPage />);
    act(() => push({ ...IDLE_STATE, mode: 'payment', lines: [{ name: 'Tea', quantity: 1, lineTotal: 90 }], subtotal: 90, total: 90 }));
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
});
