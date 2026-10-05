import { act, fireEvent, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { DEFAULT_CONFIG, useHardwareConfig } from '../../hardware/config';
import { makeCart, makeLine } from '../../test/pos';
import { renderPage, signInAs } from '../../test/render';
import { tenantApi } from '../tenant/api';
import { KioskDonePage } from './KioskDonePage';
import { KioskSlipSettings } from './KioskSlipSettings';
import { useKioskStore } from './kioskStore';
import { slipFromOrder, testSlip } from './slipData';

vi.mock('../tenant/api', () => ({ tenantApi: { get: vi.fn() } }));
vi.mock('./api', () => ({ kioskApi: { branding: vi.fn() }, deviceApi: {}, displayApi: {} }));

const print = vi.fn();
const other = [{ path: '/kiosk', element: <p>Kiosk start</p> }];

beforeEach(() => {
  vi.clearAllMocks();
  window.localStorage.clear();
  window.print = print;
  signInAs('Kiosk', { device_id: 'k1', branch_id: 'b1' });
  vi.mocked(tenantApi.get).mockResolvedValue({ name: 'Mang Inasal' } as Awaited<ReturnType<typeof tenantApi.get>>);
  useHardwareConfig.setState({ ...DEFAULT_CONFIG });
  useKioskStore.getState().resetCheckout();
});
afterEach(() => {
  document.documentElement.classList.remove('printing-kiosk-slip');
});

/** The browser reports that the print dialog closed. */
const finishPrinting = () => act(() => window.dispatchEvent(new Event('afterprint')));

describe('slip data', () => {
  it('lists the items with their choices, the total, and how the customer will pay', () => {
    const order = makeCart({
      kioskPrepNumber: 42,
      orderType: 'Take Out',
      totalAmount: 248,
      kioskPaymentPreference: 'discount',
      kioskDiscountHint: 'senior',
      lines: [makeLine({ id: 'a', itemName: 'Chicken Meal', quantity: 2, modifierSelections: [{ itemModifierId: 'm', modifierName: 'Coke Zero', modifierGroupName: 'Add drinks', priceDelta: 25 }] })],
    });
    expect(slipFromOrder(order, 'Mang Inasal', { orderType: null, payment: null, discountHint: null })).toMatchObject({
      businessName: 'Mang Inasal',
      orderNumber: '42',
      orderType: 'Take Out',
      payment: 'With Discounts: Senior Citizen',
      discounted: true,
      total: 248,
      lines: [{ quantity: 2, name: 'Chicken Meal', details: ['Coke Zero'] }],
    });
  });

  it('marks a test slip as a test so it is never mistaken for an order', () => {
    expect(testSlip('Mang Inasal').test).toBe(true);
  });
});

describe('KioskSlipSettings', () => {
  it('keeps the slip switch off until a test slip has been printed and confirmed', async () => {
    renderPage(<KioskSlipSettings />);
    const toggle = screen.getByRole('switch', { name: /Print an order slip/ });
    expect(toggle).toBeDisabled();
    expect(screen.queryByRole('button', { name: 'It printed correctly' })).toBeNull();

    fireEvent.click(screen.getByRole('button', { name: 'Print test slip' }));
    await waitFor(() => expect(print).toHaveBeenCalledTimes(1));
    finishPrinting();

    fireEvent.click(await screen.findByRole('button', { name: 'It printed correctly' }));
    expect(screen.getByRole('switch', { name: /Print an order slip/ })).toBeEnabled();

    fireEvent.click(screen.getByRole('switch', { name: /Print an order slip/ }));
    expect(useHardwareConfig.getState()).toMatchObject({ kioskPrintSlip: true, kioskSlipConfirmedWidth: 'mm80' });
  });

  it('prints only the slip: the page is marked as a slip print while the dialog is open, then restored', async () => {
    renderPage(<KioskSlipSettings />);
    fireEvent.click(screen.getByRole('button', { name: 'Print test slip' }));
    await waitFor(() => expect(print).toHaveBeenCalled());
    expect(document.documentElement).toHaveClass('printing-kiosk-slip');
    expect(document.querySelector('.kiosk-slip')).toHaveTextContent('TEST PRINT');

    finishPrinting();
    expect(document.documentElement).not.toHaveClass('printing-kiosk-slip');
    expect(document.querySelector('.kiosk-slip')).toBeNull();
  });

  it('switches the slip off and asks for a new test when the paper is changed', () => {
    useHardwareConfig.setState({ ...DEFAULT_CONFIG, kioskPrintSlip: true, kioskSlipConfirmedWidth: 'mm80' });
    renderPage(<KioskSlipSettings />);
    expect(screen.getByRole('switch', { name: /Print an order slip/ })).toBeChecked();

    fireEvent.change(screen.getByLabelText('Paper width'), { target: { value: 'mm58' } });
    expect(screen.getByRole('switch', { name: /Print an order slip/ })).toBeDisabled();
    expect(screen.getByRole('switch', { name: /Print an order slip/ })).not.toBeChecked();
  });
});

describe('KioskDonePage printing', () => {
  const order = makeCart({ kioskPrepNumber: 7, orderType: 'Dine In', lines: [makeLine({ id: 'a', itemName: 'Chicken Meal' })], totalAmount: 99, kioskPaymentPreference: 'cash' });
  const route = { route: '/kiosk/done', path: '/kiosk/done', otherRoutes: other };

  it('prints nothing and says to show the order number when the slip is not set up', async () => {
    useKioskStore.setState({ submitted: order });
    renderPage(<KioskDonePage />, route);
    expect(await screen.findByText('Show your order number at the counter.')).toBeInTheDocument();
    expect(print).not.toHaveBeenCalled();
    expect(screen.queryByRole('button', { name: 'Print again' })).toBeNull();
  });

  it('prints the order slip once when it is set up, can print it again, and says to take it to the counter', async () => {
    useHardwareConfig.setState({ ...DEFAULT_CONFIG, kioskPrintSlip: true, kioskSlipConfirmedWidth: 'mm80' });
    useKioskStore.setState({ submitted: order });
    renderPage(<KioskDonePage />, route);

    await waitFor(() => expect(print).toHaveBeenCalledTimes(1));
    expect(document.querySelector('.kiosk-slip')).toHaveTextContent('Chicken Meal');
    expect(document.querySelector('.kiosk-slip')).toHaveTextContent('Order number');
    expect(screen.getByText('Take your order slip to the counter.')).toBeInTheDocument();
    finishPrinting();

    fireEvent.click(screen.getByRole('button', { name: 'Print again' }));
    await waitFor(() => expect(print).toHaveBeenCalledTimes(2));
  });

  it('still shows the confirmation when the printer fails', async () => {
    print.mockImplementation(() => {
      throw new Error('no printer');
    });
    useHardwareConfig.setState({ ...DEFAULT_CONFIG, kioskPrintSlip: true, kioskSlipConfirmedWidth: 'mm80' });
    useKioskStore.setState({ submitted: order });
    renderPage(<KioskDonePage />, route);

    await waitFor(() => expect(print).toHaveBeenCalled());
    expect(screen.getByLabelText('Order number 7')).toBeInTheDocument();
    expect(document.documentElement).not.toHaveClass('printing-kiosk-slip');
  });
});
