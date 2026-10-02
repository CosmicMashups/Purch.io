import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { makeCart, makeLine } from '../../test/pos';
import { renderPage, signInAs } from '../../test/render';
import { branchesApi } from '../branches/api';
import { creditApi } from '../credit/api';
import { posApi } from './api';
import { PaymentPage } from './PaymentPage';
import { usePosStore } from './posStore';
import { ReceiptPage } from './ReceiptPage';

vi.mock('./api', () => ({ posApi: { getCart: vi.fn(), pay: vi.fn(), refund: vi.fn() } }));
vi.mock('../branches/api', () => ({ branchesApi: { list: vi.fn() } }));
vi.mock('../credit/api', () => ({ creditApi: { list: vi.fn() } }));

const cartWithTotal = (totalAmount: number) =>
  makeCart({ lines: [makeLine({ id: 'l1', itemName: 'Iced Latte', lineTotal: totalAmount })], subtotal: totalAmount, totalAmount });

const completed = makeCart({
  status: 2,
  receiptNumber: 41,
  lines: [makeLine({ id: 'l1', itemName: 'Iced Latte', lineTotal: 137.5 })],
  subtotal: 137.5,
  totalAmount: 137.5,
  payments: [{ id: 'p1', method: 0, status: 1, amount: 137.5, amountTendered: 200, changeGiven: 62.5 }],
});

function renderPayment() {
  return renderPage(<PaymentPage />, {
    route: '/sell/payment',
    path: '/sell/payment',
    otherRoutes: [
      { path: '/sell/receipt', element: <ReceiptPage /> },
      { path: '/sell', element: <p>Sell home</p> },
    ],
  });
}

beforeEach(() => {
  vi.clearAllMocks();
  usePosStore.setState({ receipt: null });
  signInAs('Cashier', { device_id: 'dev1', branch_id: 'kat' });
  vi.mocked(posApi.getCart).mockResolvedValue(cartWithTotal(137.5));
  vi.mocked(branchesApi.list).mockResolvedValue([
    { id: 'kat', name: 'Katipunan', address: null, manualGcashQrImageUrl: 'https://cdn.example/qr.png', manualGcashAccountName: 'Kape Katipunan', manualGcashAccountNumber: '0917 555 0101' },
  ]);
  vi.mocked(creditApi.list).mockResolvedValue([]);
});

describe('PaymentPage', () => {
  it('shows the server total and only the methods the API accepts', async () => {
    renderPayment();
    expect(await screen.findByTestId('amount-due')).toHaveTextContent('₱137.50');
    expect(screen.getAllByRole('radio').map((r) => r.textContent)).toEqual(['Cash', 'Bank transfer', 'GCash (manual QR)', 'Utang / Credit']);
    expect(screen.getByText(/QR Ph: Needs a live payment connection/)).toBeInTheDocument();
  });

  it('only allows confirming cash once the tender covers the total, and previews the change', async () => {
    renderPayment();
    const confirm = await screen.findByRole('button', { name: 'Confirm ₱137.50' });
    expect(confirm).toBeDisabled();

    fireEvent.change(screen.getByLabelText('Cash received'), { target: { value: '100' } });
    expect(confirm).toBeDisabled();
    expect(screen.getByText(/Still needs ₱37\.50/)).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText('Cash received'), { target: { value: '200' } });
    expect(confirm).toBeEnabled();
    expect(screen.getByText('Change: ₱62.50')).toBeInTheDocument();
  });

  it('fills the tender from a quick amount and the keypad', async () => {
    renderPayment();
    fireEvent.click(await screen.findByRole('button', { name: 'Exact ₱137.50' }));
    expect(screen.getByLabelText('Cash received')).toHaveValue('137.5');

    fireEvent.click(screen.getByRole('button', { name: 'Delete last digit' }));
    expect(screen.getByLabelText('Cash received')).toHaveValue('137.');
    fireEvent.click(screen.getByRole('button', { name: '9' }));
    expect(screen.getByLabelText('Cash received')).toHaveValue('137.9');
  });

  it('pays in cash, then shows the receipt and starts a new sale', async () => {
    vi.mocked(posApi.pay).mockResolvedValue(completed);
    renderPayment();
    fireEvent.change(await screen.findByLabelText('Cash received'), { target: { value: '200' } });
    fireEvent.click(screen.getByRole('button', { name: 'Confirm ₱137.50' }));

    await waitFor(() => expect(posApi.pay).toHaveBeenCalledWith({ method: 0, amountTendered: 200, customerCreditLedgerId: null, expectedTotal: 137.5 }));
    expect(await screen.findByText('Sale complete')).toBeInTheDocument();
    expect(screen.getByText('OR No. 00000041')).toBeInTheDocument();
    expect(screen.getByText('Change')).toBeInTheDocument();
    expect(screen.getByText('₱62.50')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'New sale' }));
    expect(await screen.findByText('Sell home')).toBeInTheDocument();
    expect(usePosStore.getState().receipt).toBeNull();
  });

  it('confirms a bank transfer without a tender', async () => {
    vi.mocked(posApi.pay).mockResolvedValue({ ...completed, payments: [{ ...completed.payments[0], method: 2, amountTendered: null, changeGiven: null }] });
    renderPayment();
    fireEvent.click(await screen.findByRole('radio', { name: 'Bank transfer' }));
    fireEvent.click(screen.getByRole('button', { name: 'Confirm ₱137.50' }));
    await waitFor(() => expect(posApi.pay).toHaveBeenCalledWith({ method: 2, amountTendered: null, customerCreditLedgerId: null, expectedTotal: 137.5 }));
  });

  it("shows the branch's GCash QR and account", async () => {
    renderPayment();
    fireEvent.click(await screen.findByRole('radio', { name: 'GCash (manual QR)' }));
    expect(await screen.findByAltText('GCash QR code for this branch')).toHaveAttribute('src', 'https://cdn.example/qr.png');
    expect(screen.getByText('Kape Katipunan, 0917 555 0101')).toBeInTheDocument();
  });

  it('needs a customer for Utang and sends the chosen account', async () => {
    vi.mocked(creditApi.list).mockResolvedValue([
      { id: 'c1', customerFullName: 'Aling Nena', customerPhoneNumber: '0917', customerAddress: null, balance: 200, creditLimit: 1000, dueDate: null, isActive: true },
      { id: 'c2', customerFullName: 'Closed Account', customerPhoneNumber: '0918', customerAddress: null, balance: 0, creditLimit: 500, dueDate: null, isActive: false },
    ]);
    vi.mocked(posApi.pay).mockResolvedValue(completed);
    renderPayment();
    fireEvent.click(await screen.findByRole('radio', { name: 'Utang / Credit' }));

    const confirm = screen.getByRole('button', { name: 'Confirm ₱137.50' });
    expect(confirm).toBeDisabled();
    const select = await screen.findByLabelText('Customer account');
    expect(screen.queryByText(/Closed Account/)).not.toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'Aling Nena (₱800.00 available)' })).toBeInTheDocument();

    fireEvent.change(select, { target: { value: 'c1' } });
    fireEvent.click(confirm);
    await waitFor(() => expect(posApi.pay).toHaveBeenCalledWith({ method: 5, amountTendered: null, customerCreditLedgerId: 'c1', expectedTotal: 137.5 }));
  });

  it('goes back to Cashier when the cart is empty', async () => {
    vi.mocked(posApi.getCart).mockResolvedValue(makeCart());
    renderPayment();
    expect(await screen.findByText('Sell home')).toBeInTheDocument();
  });

  it('does not clear the cart or show a receipt when the server refuses the payment', async () => {
    const { ApiError } = await import('../../lib/apiError');
    vi.mocked(posApi.pay).mockRejectedValue(new ApiError('validation', 'This customer is over their credit limit.'));
    renderPayment();
    fireEvent.change(await screen.findByLabelText('Cash received'), { target: { value: '500' } });
    fireEvent.click(screen.getByRole('button', { name: 'Confirm ₱137.50' }));
    await waitFor(() => expect(posApi.pay).toHaveBeenCalledTimes(1));
    expect(screen.queryByText('Sale complete')).not.toBeInTheDocument();
    expect(usePosStore.getState().receipt).toBeNull();
    expect(screen.getByRole('button', { name: 'Confirm ₱137.50' })).toBeEnabled();
  });
});

describe('PaymentPage when the total on the server has moved', () => {
  it('shows the server total again and charges nothing when the sale is refused for a changed price', async () => {
    const { ApiError } = await import('../../lib/apiError');
    vi.mocked(posApi.pay).mockRejectedValue(new ApiError('conflict', 'Prices or promos changed: the total is now 150.00 (the device showed 137.50). Review the cart and try again.'));
    renderPayment();
    fireEvent.change(await screen.findByLabelText('Cash received'), { target: { value: '500' } });

    // The next read of the cart comes back at the new price.
    vi.mocked(posApi.getCart).mockResolvedValue(cartWithTotal(150));
    fireEvent.click(screen.getByRole('button', { name: 'Confirm ₱137.50' }));

    await waitFor(() => expect(posApi.pay).toHaveBeenCalledWith(expect.objectContaining({ expectedTotal: 137.5 })));
    expect(await screen.findByTestId('amount-due')).toHaveTextContent('₱150.00');
    expect(screen.queryByText('Sale complete')).not.toBeInTheDocument();
    expect(usePosStore.getState().receipt).toBeNull();
  });
});

describe('ReceiptPage', () => {
  it('sends you back to Cashier if there is no completed sale to show', async () => {
    renderPage(<ReceiptPage />, { route: '/sell/receipt', path: '/sell/receipt', otherRoutes: [{ path: '/sell', element: <p>Sell home</p> }] });
    expect(await screen.findByText('Sell home')).toBeInTheDocument();
  });

  it('lays the receipt out as an official receipt with the VAT breakdown', () => {
    usePosStore.setState({ receipt: completed });
    renderPage(<ReceiptPage />);
    expect(screen.getByText('OFFICIAL RECEIPT')).toBeInTheDocument();
    expect(screen.getByText('VATable Sales')).toBeInTheDocument();
    expect(screen.getByText('THIS SERVES AS YOUR OFFICIAL RECEIPT')).toBeInTheDocument();
  });

  it('refunds the sale once a reason and an approver PIN are given, and shows it as refunded', async () => {
    const { ApiError } = await import('../../lib/apiError');
    usePosStore.setState({ receipt: completed });
    vi.mocked(posApi.refund).mockImplementation((_id, body) =>
      body.approverPin === '5678'
        ? Promise.resolve({ ...completed, status: 4 })
        : Promise.reject(new ApiError('validation', 'That PIN doesn\'t match a different active manager or admin.', { approverPin: ['That PIN doesn\'t match a different active manager or admin.'] })),
    );
    renderPage(<ReceiptPage />);

    fireEvent.click(screen.getByRole('button', { name: 'Refund' }));
    const dialog = await screen.findByRole('dialog', { name: /Refund/ });
    fireEvent.change(within(dialog).getByLabelText('Reason'), { target: { value: 'Customer changed their mind' } });
    fireEvent.change(within(dialog).getByLabelText('Manager or admin PIN'), { target: { value: '0000' } });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Refund' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent("That PIN doesn't match a different active manager or admin.");
    fireEvent.change(within(dialog).getByLabelText('Manager or admin PIN'), { target: { value: '5678' } });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Refund' }));

    await waitFor(() =>
      expect(posApi.refund).toHaveBeenLastCalledWith('cart1', { reason: 'Customer changed their mind', approverPin: '5678' }),
    );
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(screen.getByText('*** REFUNDED ***')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Refund' })).not.toBeInTheDocument();
  });

  it('cannot submit the refund dialog until both a reason and a PIN are entered', async () => {
    usePosStore.setState({ receipt: completed });
    renderPage(<ReceiptPage />);
    fireEvent.click(screen.getByRole('button', { name: 'Refund' }));
    const dialog = await screen.findByRole('dialog', { name: /Refund/ });
    expect(within(dialog).getByRole('button', { name: 'Refund' })).toBeDisabled();
  });
});
