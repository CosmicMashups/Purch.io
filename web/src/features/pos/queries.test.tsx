import { renderHook, waitFor, act } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

vi.mock('../../lib/apiClient', () => ({
  apiClient: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
}));

import { apiClient } from '../../lib/apiClient';
import { posApi } from './api';
import { posKeys, useParkCart, useParkedCarts, usePay, useResumeCart } from './queries';
import { PaymentMethod } from './types';

const client = apiClient as unknown as Record<'get' | 'post' | 'put' | 'delete', ReturnType<typeof vi.fn>>;

function setup() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  const wrapper = ({ children }: { children: ReactNode }) => <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
  return { qc, wrapper };
}

beforeEach(() => {
  Object.values(client).forEach((f) => f.mockReset());
});

describe('parked carts', () => {
  it('resume writes the returned cart into the cart cache and invalidates the parked list', async () => {
    const { qc, wrapper } = setup();
    const cart = { id: 'c1', lines: [], changedLines: [{ lineId: 'l1', reason: 'PriceChanged' }] };
    client.post.mockResolvedValue({ data: cart });
    const spy = vi.spyOn(qc, 'invalidateQueries');
    const { result } = renderHook(() => useResumeCart(), { wrapper });
    await act(async () => {
      await result.current.mutateAsync('c1');
    });
    expect(client.post).toHaveBeenCalledWith('/transactions/c1/resume');
    expect(qc.getQueryData(posKeys.cart)).toEqual(cart);
    expect(spy).toHaveBeenCalledWith({ queryKey: ['pos', 'parked'] });
  });

  it('park posts the label, drops the cart cache and invalidates the parked list', async () => {
    const { qc, wrapper } = setup();
    client.post.mockResolvedValue({ data: { id: 'c1' } });
    qc.setQueryData(posKeys.cart, { id: 'c1' });
    const spy = vi.spyOn(qc, 'invalidateQueries');
    const { result } = renderHook(() => useParkCart(), { wrapper });
    await act(async () => {
      await result.current.mutateAsync('Table 4');
    });
    expect(client.post).toHaveBeenCalledWith('/transactions/cart/park', { label: 'Table 4' });
    expect(qc.getQueryData(posKeys.cart)).toBeUndefined();
    expect(spy).toHaveBeenCalledWith({ queryKey: ['pos', 'parked'] });
  });

  it('lists parked carts under the parked key', async () => {
    const { wrapper } = setup();
    client.get.mockResolvedValue({ data: [{ id: 'p1' }] });
    const { result } = renderHook(() => useParkedCarts(true), { wrapper });
    await waitFor(() => expect(result.current.data).toEqual([{ id: 'p1' }]));
    expect(client.get).toHaveBeenCalledWith('/transactions/parked');
  });
});

describe('posApi routes', () => {
  beforeEach(() => {
    client.post.mockResolvedValue({ data: {} });
    client.put.mockResolvedValue({ data: {} });
    client.delete.mockResolvedValue({ data: {} });
  });
  it('maps each new call to the backend route', async () => {
    await posApi.discardParked('p1', '1234');
    expect(client.delete).toHaveBeenCalledWith('/transactions/p1/parked', { data: { approverPin: '1234' } });
    await posApi.setLineDiscount('l1', { kind: 'Percent', value: 10, reason: 'x' });
    expect(client.put).toHaveBeenCalledWith('/transactions/cart/lines/l1/discount', { kind: 'Percent', value: 10, reason: 'x' });
    await posApi.setCartDiscount({ kind: 'Fixed', value: 5, reason: 'y' });
    expect(client.put).toHaveBeenCalledWith('/transactions/cart/discount', { kind: 'Fixed', value: 5, reason: 'y' });
    await posApi.overrideLinePrice('l1', { newUnitPrice: 9, reason: 'z' });
    expect(client.put).toHaveBeenCalledWith('/transactions/cart/lines/l1/price', { newUnitPrice: 9, reason: 'z' });
    await posApi.addOpenItem({ name: 'Misc', unitPrice: 5, quantity: 1 });
    expect(client.post).toHaveBeenCalledWith('/transactions/cart/lines/open', { name: 'Misc', unitPrice: 5, quantity: 1 });
    await posApi.setLineNote('l1', 'no ice', '1234');
    expect(client.put).toHaveBeenCalledWith('/transactions/cart/lines/l1/note', { note: 'no ice', approverPin: '1234' });
  });
});

describe('usePay', () => {
  it('posts parts when given', async () => {
    const { wrapper } = setup();
    client.post.mockResolvedValue({ data: { id: 't' } });
    const { result } = renderHook(() => usePay(), { wrapper });
    const parts = [
      { method: PaymentMethod.Cash, amount: 50, amountTendered: 100 },
      { method: PaymentMethod.BankTransfer, amount: 50, reference: 'r1' },
    ];
    await act(async () => {
      await result.current.mutateAsync({ method: PaymentMethod.Split, amountTendered: null, expectedTotal: 100, parts });
    });
    expect(client.post).toHaveBeenCalledWith('/transactions/cart/payments', expect.objectContaining({ parts }));
  });

  it('keeps the legacy single-tender body when no parts', async () => {
    const { wrapper } = setup();
    client.post.mockResolvedValue({ data: { id: 't' } });
    const { result } = renderHook(() => usePay(), { wrapper });
    await act(async () => {
      await result.current.mutateAsync({ method: PaymentMethod.Cash, amountTendered: 100, expectedTotal: 80 });
    });
    const body = client.post.mock.calls[0][1];
    expect(body).toEqual({ method: PaymentMethod.Cash, amountTendered: 100, expectedTotal: 80 });
    expect(body).not.toHaveProperty('parts');
  });
});
