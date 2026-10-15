import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderPage, signInAs } from '../../../test/render';
import { inventoryApi } from '../api';
import { PurchaseRequestStatus, type PurchaseRequest } from '../types';
import { PurchaseRequestsPanel } from './PurchaseRequestsPanel';

vi.mock('../api', () => ({
  MOVEMENT_PAGE_SIZE: 30,
  inventoryApi: {
    listPurchaseRequests: vi.fn(),
    listSuppliers: vi.fn(),
    listRestockSuggestions: vi.fn(),
    approvePurchaseRequest: vi.fn(),
    cancelPurchaseRequest: vi.fn(),
    convertPurchaseRequest: vi.fn(),
  },
}));

const request = (status: PurchaseRequestStatus): PurchaseRequest => ({
  id: 'pr1',
  controlNumber: 'MAIN-PR-00001',
  branchId: 'b1',
  branchName: 'Main',
  requestDate: '2026-10-15',
  dateNeeded: null,
  requestedByName: 'Wanda Warehouse',
  deliveryAddress: '1 Rizal St',
  remarks: null,
  status,
  lines: [
    { id: 'l1', itemId: 'rice', inventoryItemId: null, name: 'Rice', quantity: 10, unit: 'kg' },
    { id: 'l2', itemId: 'oil', inventoryItemId: null, name: 'Oil', quantity: 4, unit: 'pc' },
  ],
});

describe('PurchaseRequestsPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(inventoryApi.listSuppliers).mockResolvedValue([
      { id: 's1', name: 'Alpha Foods', contactInfo: null, isActive: true, specialization: null, address: null, tin: null, remarks: null, contacts: [] },
      { id: 's2', name: 'Beta Trading', contactInfo: null, isActive: true, specialization: null, address: null, tin: null, remarks: null, contacts: [] },
    ]);
    vi.mocked(inventoryApi.listRestockSuggestions).mockResolvedValue([
      {
        itemId: 'rice',
        inventoryItemId: null,
        name: 'Rice',
        onHand: 0,
        threshold: 10,
        suggestedQuantity: 10,
        unit: 'kg',
        unitCost: null,
        suppliers: [{ supplierId: 's1', supplierName: 'Alpha Foods', isPreferred: true, unitCost: null }],
        pending: null,
      },
    ]);
  });

  it('shows the control number, requester and delivery address', async () => {
    signInAs('Warehouse');
    vi.mocked(inventoryApi.listPurchaseRequests).mockResolvedValue([request(PurchaseRequestStatus.Open)]);
    renderPage(<PurchaseRequestsPanel />);
    await screen.findByText('MAIN-PR-00001');
    expect(screen.getByText(/Wanda Warehouse/)).toBeInTheDocument();
    expect(screen.getByRole('row', { name: 'Open MAIN-PR-00001' })).toBeInTheDocument();
    expect(screen.getByText(/\d+ items?/)).toBeInTheDocument();
  });

  it('lets only an Admin or Manager approve', async () => {
    signInAs('Warehouse');
    vi.mocked(inventoryApi.listPurchaseRequests).mockResolvedValue([request(PurchaseRequestStatus.Open)]);
    const warehouse = renderPage(<PurchaseRequestsPanel />);
    await screen.findByText('MAIN-PR-00001');
    fireEvent.click(screen.getByRole('button', { name: /^Actions for MAIN-PR-00001/ }));
    expect(screen.queryByRole('menuitem', { name: 'Approve' })).not.toBeInTheDocument();
    expect(screen.getByRole('menuitem', { name: 'Cancel request' })).toBeInTheDocument();
    warehouse.unmount();

    signInAs('Manager');
    vi.mocked(inventoryApi.approvePurchaseRequest).mockResolvedValue({} as never);
    renderPage(<PurchaseRequestsPanel />);
    fireEvent.click(await screen.findByRole('button', { name: /^Actions for MAIN-PR-00001/ }));
    fireEvent.click(await screen.findByRole('menuitem', { name: 'Approve' }));
    await waitFor(() => expect(inventoryApi.approvePurchaseRequest).toHaveBeenCalledWith('pr1'));
  });

  it('converts an approved request with the preferred supplier pre-selected and groups lines by supplier', async () => {
    signInAs('Warehouse');
    vi.mocked(inventoryApi.listPurchaseRequests).mockResolvedValue([request(PurchaseRequestStatus.Approved)]);
    vi.mocked(inventoryApi.convertPurchaseRequest).mockResolvedValue([] as never);
    renderPage(<PurchaseRequestsPanel />);
    fireEvent.click(await screen.findByRole('button', { name: /^Actions for MAIN-PR-00001/ }));
    fireEvent.click(await screen.findByRole('menuitem', { name: 'Create purchase orders' }));

    const dialog = await screen.findByRole('dialog');
    const rice = within(dialog).getByLabelText('Supplier for Rice') as HTMLSelectElement;
    expect(rice.value).toBe('s1');
    const oil = within(dialog).getByLabelText('Supplier for Oil') as HTMLSelectElement;
    expect(oil.value).toBe('');
    fireEvent.change(oil, { target: { value: 's2' } });
    fireEvent.change(within(dialog).getByLabelText('Unit price for Rice'), { target: { value: '55.5' } });
    fireEvent.change(within(dialog).getByLabelText('Unit price for Oil'), { target: { value: '120' } });

    fireEvent.click(within(dialog).getByRole('button', { name: 'Create orders' }));
    await waitFor(() =>
      expect(inventoryApi.convertPurchaseRequest).toHaveBeenCalledWith('pr1', {
        orders: [
          { supplierId: 's1', lineIds: ['l1'], vatInclusive: false, discountAmount: 0, unitPrices: { l1: 55.5 } },
          { supplierId: 's2', lineIds: ['l2'], vatInclusive: false, discountAmount: 0, unitPrices: { l2: 120 } },
        ],
      }),
    );
  });
});
