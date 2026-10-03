import { act, render } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { signInAs } from '../../test/render';
import { useAuthStore } from '../../lib/authStore';
import { IDLE_STATE, type CustomerDisplayState } from './channel';
import * as serverFeed from './serverFeed';
import { usePublishCustomerDisplay } from './usePublishCustomerDisplay';

function b64(o: object) {
  return btoa(JSON.stringify(o)).replace(/=+$/, '').replace(/\+/g, '-').replace(/\//g, '_');
}

function Probe({ state }: { state: CustomerDisplayState }) {
  usePublishCustomerDisplay(state);
  return null;
}

const cart = (total: number): CustomerDisplayState => ({ ...IDLE_STATE, mode: 'cart', total, subtotal: total });

beforeEach(() => {
  vi.useFakeTimers();
  useAuthStore.setState({ accessToken: null, refreshToken: null });
});
afterEach(() => {
  vi.useRealTimers();
  vi.restoreAllMocks();
});

describe('usePublishCustomerDisplay', () => {
  it('tells the paired customer display once a burst of changes has settled, on a Register', () => {
    const publish = vi.spyOn(serverFeed, 'publishToCustomerDisplay').mockImplementation(() => undefined);
    useAuthStore.setState({ accessToken: `${b64({ alg: 'none' })}.${b64({ role: 'Cashier', tenant_id: 't1', device_id: 'd1' })}.x`, refreshToken: 'r' });
    const { rerender } = render(<Probe state={cart(100)} />);
    rerender(<Probe state={cart(250)} />);
    rerender(<Probe state={cart(400)} />);
    expect(publish).not.toHaveBeenCalled();

    act(() => vi.advanceTimersByTime(300));
    expect(publish).toHaveBeenCalledTimes(1);
    expect(publish).toHaveBeenCalledWith(expect.objectContaining({ total: 400 }));
  });

  it('sends nothing to the server from a session that is not on a Register', () => {
    const publish = vi.spyOn(serverFeed, 'publishToCustomerDisplay').mockImplementation(() => undefined);
    signInAs('Admin');
    render(<Probe state={cart(100)} />);
    act(() => vi.advanceTimersByTime(1000));
    expect(publish).not.toHaveBeenCalled();
  });
});
