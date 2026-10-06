import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderPage, signInAs } from '../../../test/render';
import { branchesApi } from '../../branches/api';
import { catalogApi } from '../../catalog/api';
import type { Item } from '../../catalog/types';
import { inventoryApi } from '../api';
import { PurchaseOrderStatus, ReceivingCondition, ReceivingRemark, type IncomingReceiving, type PurchaseOrder } from '../types';
import { IncomingReceivingPage } from './IncomingReceivingPage';

vi.mock('../../branches/api', () => ({ branchesApi: { list: vi.fn() } }));
vi.mock('../../catalog/api', () => ({ catalogApi: { listItems: vi.fn() } }));
vi.mock('../api', () => ({
  MOVEMENT_PAGE_SIZE: 30,
  inventoryApi: {
    listIncomingReceiving: vi.fn(),
    createIncomingReceiving: vi.fn(),
    linkIncomingReceiving: vi.fn(),
    listPurchaseOrders: vi.fn(),
    listSuppliers: vi.fn(),
  },
}));

const report: IncomingReceiving = {
  id: 'r1',
  purchaseOrderId: null,
  supplierId: 's1',
  supplierName: 'Metro Foods',
  branchId: 'kat',
  branchName: 'Katipunan',
  receivedByUserId: 'u1',
  receivedByName: 'Joy',
  deliveryDate: '2026-10-05',
  remarks: null,
  createdAt: '2026-10-05T01:00:00Z',
  lines: [
    { id: 'l1', itemId: 'milk', itemName: 'Milk', quantityReceived: 10, uom: 'box', unitPrice: 80, condition: ReceivingCondition.NotGood, remark: ReceivingRemark.Rejected },
  ],
};

const order: PurchaseOrder = {
  id: 'po1',
  supplierId: 's1',
  supplierName: 'Metro Foods',
  branchId: 'kat',
  branchName: 'Katipunan',
  status: PurchaseOrderStatus.Sent,
  sentAt: null,
  receipts: [],
  lines: [{ id: 'pl1', itemId: 'milk', itemName: 'Milk', quantityOrdered: 10, quantityReceived: 0, expectedUnitCost: 80 }],
};

describe('IncomingReceivingPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    signInAs('Admin', { scope_type: 'Tenant' });
    vi.mocked(catalogApi.listItems).mockResolvedValue([{ id: 'milk', name: 'Milk' }] as Item[]);
    vi.mocked(branchesApi.list).mockResolvedValue([{ id: 'kat', name: 'Katipunan', address: null }]);
    vi.mocked(inventoryApi.listSuppliers).mockResolvedValue([
      { id: 's1', name: 'Metro Foods', contactInfo: null, isActive: true, specialization: null, address: null, tin: null, remarks: null, contacts: [] },
    ]);
    vi.mocked(inventoryApi.listPurchaseOrders).mockResolvedValue([order]);
    vi.mocked(inventoryApi.listIncomingReceiving).mockResolvedValue([report]);
  });

  it('shows who received a delivery, its lines, and that it is not linked yet', async () => {
    renderPage(<IncomingReceivingPage />);
    expect(await screen.findByText(/Received by Joy/)).toBeInTheDocument();
    expect(screen.getByText('Not linked')).toBeInTheDocument();
    expect(screen.getAllByText('Rejected').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Not good').length).toBeGreaterThan(0);
  });

  it('links an unlinked report to a matching open order', async () => {
    vi.mocked(inventoryApi.linkIncomingReceiving).mockResolvedValue({} as never);
    renderPage(<IncomingReceivingPage />);
    await screen.findByText('Not linked');
    fireEvent.change(screen.getByLabelText('Link to purchase order'), { target: { value: 'po1' } });
    fireEvent.click(screen.getByRole('button', { name: 'Link' }));
    await waitFor(() => expect(inventoryApi.linkIncomingReceiving).toHaveBeenCalledWith('r1', 'po1'));
  });

  it('records a delivery without a purchase order', async () => {
    vi.mocked(inventoryApi.createIncomingReceiving).mockResolvedValue({} as never);
    renderPage(<IncomingReceivingPage />);
    await screen.findByText('Not linked');
    expect(screen.queryByLabelText('Supplier')).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Record a delivery' }));
    const dialog = await screen.findByRole('dialog', { name: 'Record a delivery' });
    fireEvent.change(await within(dialog).findByLabelText('Supplier'), { target: { value: 's1' } });
    fireEvent.change(within(dialog).getByLabelText('Item'), { target: { value: 'milk' } });
    fireEvent.change(within(dialog).getByLabelText('Quantity'), { target: { value: '5' } });
    fireEvent.change(within(dialog).getByLabelText('Unit price (PHP)'), { target: { value: '12' } });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save report' }));
    await waitFor(() => expect(inventoryApi.createIncomingReceiving).toHaveBeenCalledTimes(1));
    expect(vi.mocked(inventoryApi.createIncomingReceiving).mock.calls[0][0]).toMatchObject({
      purchaseOrderId: null,
      supplierId: 's1',
      branchId: 'kat',
      lines: [{ itemId: 'milk', quantityReceived: 5, uom: 'pc', unitPrice: 12, condition: ReceivingCondition.Good, remark: ReceivingRemark.Accepted }],
    });
  });

  it('prefills supplier, branch and lines when a purchase order is chosen', async () => {
    renderPage(<IncomingReceivingPage />);
    await screen.findByText('Not linked');
    fireEvent.click(screen.getByRole('button', { name: 'Record a delivery' }));
    fireEvent.change(await screen.findByLabelText('Purchase order (optional)'), { target: { value: 'po1' } });
    await waitFor(() => expect(screen.getByLabelText('Supplier')).toHaveValue('s1'));
    expect(screen.getByLabelText('Quantity')).toHaveValue(10);
  });
});
