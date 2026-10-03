import { act, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { IDLE_STATE, type CustomerDisplayState } from './channel';
import { POLL_MS, customerDisplayFeed, useServerCustomerDisplay } from './serverFeed';

const cart: CustomerDisplayState = { ...IDLE_STATE, mode: 'cart', lines: [{ name: 'Latte', quantity: 2, unitPrice: 150, lineTotal: 300 }], subtotal: 300, total: 300, vat: 32.14 };

function Probe({ enabled = true }: { enabled?: boolean }) {
  const state = useServerCustomerDisplay(enabled);
  return <p data-testid="mode">{state.mode === 'cart' ? `cart:${state.lines[0].name}` : state.mode}</p>;
}

beforeEach(() => vi.useFakeTimers());
afterEach(() => {
  vi.useRealTimers();
  vi.restoreAllMocks();
});

async function tick(ms = POLL_MS) {
  await act(async () => {
    await vi.advanceTimersByTimeAsync(ms);
  });
}

describe('useServerCustomerDisplay', () => {
  it('starts on the welcome screen and shows the order once the Register has pushed one', async () => {
    const poll = vi.spyOn(customerDisplayFeed, 'poll').mockResolvedValueOnce({ version: 0, updatedAt: null, state: null }).mockResolvedValue({ version: 1, updatedAt: null, state: cart });
    render(<Probe />);
    await tick(0);
    expect(screen.getByTestId('mode')).toHaveTextContent('idle');

    await tick();
    expect(screen.getByTestId('mode')).toHaveTextContent('cart:Latte');
    expect(poll).toHaveBeenLastCalledWith(0);
  });

  it('asks again with the version it has, and leaves the screen alone when nothing is newer', async () => {
    const poll = vi.spyOn(customerDisplayFeed, 'poll').mockResolvedValueOnce({ version: 3, updatedAt: null, state: cart }).mockResolvedValue(null);
    render(<Probe />);
    await tick(0);
    await tick();
    await tick();
    expect(poll).toHaveBeenNthCalledWith(1, null);
    expect(poll).toHaveBeenNthCalledWith(2, 3);
    expect(screen.getByTestId('mode')).toHaveTextContent('cart:Latte');
  });

  it('keeps the last order on screen through a dropped connection and carries on', async () => {
    vi.spyOn(customerDisplayFeed, 'poll').mockResolvedValueOnce({ version: 1, updatedAt: null, state: cart }).mockRejectedValueOnce(new Error('offline')).mockResolvedValue({ version: 2, updatedAt: null, state: { ...cart, lines: [{ name: 'Tea', quantity: 1, unitPrice: 90, lineTotal: 90 }] } });
    render(<Probe />);
    await tick(0);
    await tick();
    expect(screen.getByTestId('mode')).toHaveTextContent('cart:Latte');
    await tick();
    expect(screen.getByTestId('mode')).toHaveTextContent('cart:Tea');
  });

  it('does not poll when it is not a paired display, and stops polling when it goes away', async () => {
    const poll = vi.spyOn(customerDisplayFeed, 'poll').mockResolvedValue(null);
    const off = render(<Probe enabled={false} />);
    await tick(5000);
    expect(poll).not.toHaveBeenCalled();
    off.unmount();

    const on = render(<Probe />);
    await tick(0);
    const calls = poll.mock.calls.length;
    on.unmount();
    await tick(5000);
    expect(poll.mock.calls.length).toBe(calls);
  });
});
