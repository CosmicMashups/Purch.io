import { act, render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { signInAs } from '../../test/render';
import { tenantApi } from '../tenant/api';
import { KioskLayout } from './KioskLayout';
import { useKioskStore } from './kioskStore';
import { useLocalKioskCartStore } from './localCart';
import { IDLE_CLEAR_MS, IDLE_WARN_MS, idleStage } from './useKioskIdle';

vi.mock('../tenant/api', () => ({ tenantApi: { get: vi.fn() } }));
vi.mock('./api', () => ({ kioskApi: { branding: vi.fn() }, deviceApi: {}, displayApi: {} }));

const line = { localId: 'l1', itemId: 'i1', itemName: 'Iced Latte', itemVariantId: null, itemVariantAttributes: {}, quantity: 1, unitPrice: 150, comboSelections: [], modifierSelections: [] };

function renderKiosk(path: string) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route element={<KioskLayout />}>
            <Route path="/kiosk" element={<p>Welcome screen</p>} />
            <Route path="/kiosk/menu" element={<p>Menu screen</p>} />
            <Route path="/kiosk/processing" element={<p>Sending screen</p>} />
          </Route>
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

/** Lets the one-second idle check run for this many milliseconds of pretend time. */
const pass = (ms: number) => act(() => vi.advanceTimersByTimeAsync(ms));

beforeEach(() => {
  vi.useFakeTimers();
  vi.setSystemTime(new Date('2026-10-05T10:00:00Z'));
  window.sessionStorage.clear();
  signInAs('Kiosk', { device_id: 'k1', branch_id: 'b1' });
  vi.mocked(tenantApi.get).mockResolvedValue({ name: 'Mang Inasal' } as Awaited<ReturnType<typeof tenantApi.get>>);
  useKioskStore.getState().resetCheckout();
  useLocalKioskCartStore.setState({ lines: [], lastActivityAt: Date.now() });
});
afterEach(() => vi.useRealTimers());

describe('idleStage', () => {
  it('asks after 45 seconds and clears at 60 when there is an order', () => {
    expect(idleStage(IDLE_WARN_MS - 1, true)).toBe('active');
    expect(idleStage(IDLE_WARN_MS, true)).toBe('warn');
    expect(idleStage(IDLE_CLEAR_MS - 1, true)).toBe('warn');
    expect(idleStage(IDLE_CLEAR_MS, true)).toBe('expired');
  });

  it('resets an empty order quietly at 45 seconds, with no question', () => {
    expect(idleStage(IDLE_WARN_MS - 1, false)).toBe('active');
    expect(idleStage(IDLE_WARN_MS, false)).toBe('expired');
  });
});

describe('KioskLayout', () => {
  it('shows the business name and logo on every screen except the welcome screen', async () => {
    const welcome = renderKiosk('/kiosk');
    await pass(10);
    expect(screen.queryByText('Mang Inasal')).toBeNull();
    welcome.unmount();

    renderKiosk('/kiosk/menu');
    await pass(10);
    expect(screen.getByText('Mang Inasal')).toBeInTheDocument();
    expect(screen.getByAltText('Company logo')).toBeInTheDocument();
  });

  it('asks "Need more time?" after 45 idle seconds and carries on when the customer says so', async () => {
    useLocalKioskCartStore.setState({ lines: [line], lastActivityAt: Date.now() });
    renderKiosk('/kiosk/menu');
    await pass(IDLE_WARN_MS + 1_000);

    expect(screen.getByRole('dialog', { name: 'Need more time?' })).toBeInTheDocument();
    expect(screen.getByRole('timer')).toHaveTextContent(/cleared in 1[0-5]s/);

    await act(async () => screen.getByRole('button', { name: 'Yes, continue' }).click());
    expect(screen.queryByRole('dialog')).toBeNull();
    await pass(IDLE_WARN_MS - 5_000);
    expect(screen.queryByRole('dialog')).toBeNull();
    expect(useLocalKioskCartStore.getState().lines).toHaveLength(1);
  });

  it('clears the order and returns to the welcome screen when nobody answers', async () => {
    useLocalKioskCartStore.setState({ lines: [line], lastActivityAt: Date.now() });
    renderKiosk('/kiosk/menu');
    await pass(IDLE_CLEAR_MS + 2_000);

    expect(screen.getByText('Welcome screen')).toBeInTheDocument();
    expect(useLocalKioskCartStore.getState().lines).toHaveLength(0);
  });

  it('does not let a tap behind the question keep a dead order alive', async () => {
    useLocalKioskCartStore.setState({ lines: [line], lastActivityAt: Date.now() });
    renderKiosk('/kiosk/menu');
    await pass(IDLE_WARN_MS + 1_000);
    expect(screen.getByRole('dialog')).toBeInTheDocument();

    await act(async () => {
      window.dispatchEvent(new Event('pointerdown'));
    });
    await pass(IDLE_CLEAR_MS - IDLE_WARN_MS);
    expect(screen.getByText('Welcome screen')).toBeInTheDocument();
  });

  it('resets an empty order quietly after 45 seconds', async () => {
    renderKiosk('/kiosk/menu');
    await pass(IDLE_WARN_MS + 2_000);
    expect(screen.queryByRole('dialog')).toBeNull();
    expect(screen.getByText('Welcome screen')).toBeInTheDocument();
  });

  it('never times out while the order is being sent', async () => {
    useLocalKioskCartStore.setState({ lines: [line], lastActivityAt: Date.now() });
    renderKiosk('/kiosk/processing');
    await pass(IDLE_CLEAR_MS * 2);
    expect(screen.getByText('Sending screen')).toBeInTheDocument();
    expect(useLocalKioskCartStore.getState().lines).toHaveLength(1);
  });
});
