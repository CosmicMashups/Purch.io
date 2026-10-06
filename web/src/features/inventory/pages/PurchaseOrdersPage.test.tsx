import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderPage, signInAs } from '../../../test/render';
import { branchesApi } from '../../branches/api';
import { catalogApi } from '../../catalog/api';
import type { Item } from '../../catalog/types';
import { inventoryApi } from '../api';
import { PurchaseOrderStatus, type PurchaseOrder } from '../types';
import { PurchaseOrdersPage } from './PurchaseOrdersPage';

vi.mock('../../branches/api', () => ({ branchesApi: { list: vi.fn() } }));
vi.mock('../../catalog/api', () => ({ catalogApi: { listItems: vi.fn() } }));
vi.mock('../api', () => ({
  MOVEMENT_PAGE_SIZE: 30,
  inventoryApi: {
    listPurchaseOrders: vi.fn(),
    listSuppliers: vi.fn(),
    createPurchaseOrder: vi.fn(),
    markPurchaseOrderSent: vi.fn(),
    cancelPurchaseOrder: vi.fn(),
  },
}));

const order = (status: PurchaseOrderStatus, id = 'po1'): PurchaseOrder => ({
  id,
  supplierId: 's1',
  supplierName: 'Metro Foods',
  branchId: 'kat',
  branchName: 'Katipunan',
  status,
  sentAt: null,
  receipts: [],
  lines: [{ id: 'l1', itemId: 'milk', itemName: 'Milk', quantityOrdered: 10, quantityReceived: 4, expectedUnitCost: 80 }],
});

describe('PurchaseOrdersPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    signInAs('Warehouse', { scope_type: 'Tenant' });
    vi.mocked(catalogApi.listItems).mockResolvedValue([{ id: 'milk', name: 'Milk' }] as Item[]);
    vi.mocked(branchesApi.list).mockResolvedValue([{ id: 'kat', name: 'Katipunan', address: null }]);
    vi.mocked(inventoryApi.listSuppliers).mockResolvedValue([{ id: 's1', name: 'Metro Foods', contactInfo: null, isActive: true, specialization: null, address: null, tin: null, remarks: null, contacts: [] }]);
    vi.mocked(inventoryApi.listPurchaseOrders).mockResolvedValue([order(PurchaseOrderStatus.Draft)]);
  });

  it('offers only the actions the status allows', async () => {
    vi.mocked(inventoryApi.listPurchaseOrders).mockResolvedValue([
      order(PurchaseOrderStatus.Draft, 'a'),
      order(PurchaseOrderStatus.Received, 'b'),
    ]);
    renderPage(<PurchaseOrdersPage />);
    await screen.findAllByText('Deliver to Katipunan');
    expect(screen.getAllByRole('button', { name: 'Submit order' })).toHaveLength(1);
    expect(screen.getAllByRole('button', { name: 'Cancel order' })).toHaveLength(1);
    expect(screen.queryAllByRole('link', { name: 'Record delivery' })).toHaveLength(0);
  });

  it('submits a draft', async () => {
    vi.mocked(inventoryApi.markPurchaseOrderSent).mockResolvedValue({} as never);
    renderPage(<PurchaseOrdersPage />);
    fireEvent.click(await screen.findByRole('button', { name: 'Submit order' }));
    await waitFor(() => expect(inventoryApi.markPurchaseOrderSent).toHaveBeenCalledWith('po1'));
  });

  it('asks before cancelling and only cancels on confirm', async () => {
    vi.mocked(inventoryApi.cancelPurchaseOrder).mockResolvedValue({} as never);
    renderPage(<PurchaseOrdersPage />);
    fireEvent.click(await screen.findByRole('button', { name: 'Cancel order' }));

    const dialog = screen.getByRole('dialog', { name: 'Cancel this purchase order?' });
    expect(inventoryApi.cancelPurchaseOrder).not.toHaveBeenCalled();
    fireEvent.click(within(dialog).getByRole('button', { name: 'Cancel order' }));
    await waitFor(() => expect(inventoryApi.cancelPurchaseOrder).toHaveBeenCalledWith('po1'));
  });

  it('backing out of the cancel dialog changes nothing', async () => {
    renderPage(<PurchaseOrdersPage />);
    fireEvent.click(await screen.findByRole('button', { name: 'Cancel order' }));
    fireEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Cancel' }));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(inventoryApi.cancelPurchaseOrder).not.toHaveBeenCalled();
  });

  it('sends a partly delivered order to the receiving report with the order preselected', async () => {
    vi.mocked(inventoryApi.listPurchaseOrders).mockResolvedValue([order(PurchaseOrderStatus.PartiallyReceived)]);
    renderPage(<PurchaseOrdersPage />);
    const link = await screen.findByRole('link', { name: 'Record delivery' });
    expect(link).toHaveAttribute('href', '/inventory/incoming-receiving?po=po1');
    expect(screen.getByText('Partially delivered')).toBeInTheDocument();
  });

  it('creates a draft order', async () => {
    vi.mocked(inventoryApi.createPurchaseOrder).mockResolvedValue({} as never);
    renderPage(<PurchaseOrdersPage />);
    await screen.findByText('Deliver to Katipunan');
    expect(screen.queryByLabelText('Supplier')).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'New purchase order' }));
    const dialog = await screen.findByRole('dialog', { name: 'New purchase order' });

    fireEvent.change(await within(dialog).findByLabelText('Supplier'), { target: { value: 's1' } });
    fireEvent.change(within(dialog).getByLabelText('Item'), { target: { value: 'milk' } });
    fireEvent.change(within(dialog).getByLabelText('Quantity'), { target: { value: '24' } });
    fireEvent.change(within(dialog).getByLabelText('Unit cost (PHP)'), { target: { value: '80.5' } });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Create draft' }));

    await waitFor(() => expect(inventoryApi.createPurchaseOrder).toHaveBeenCalledTimes(1));
    expect(inventoryApi.createPurchaseOrder).toHaveBeenCalledWith({
      supplierId: 's1',
      branchId: 'kat',
      lines: [{ itemId: 'milk', quantityOrdered: 24, expectedUnitCost: 80.5 }],
    });
  });

  it('will not create an order without a supplier', async () => {
    renderPage(<PurchaseOrdersPage />);
    await screen.findByText('Deliver to Katipunan');
    fireEvent.click(screen.getByRole('button', { name: 'New purchase order' }));
    fireEvent.click(await screen.findByRole('button', { name: 'Create draft' }));
    expect(await screen.findByText('Choose a supplier')).toBeInTheDocument();
    expect(inventoryApi.createPurchaseOrder).not.toHaveBeenCalled();
  });
});
