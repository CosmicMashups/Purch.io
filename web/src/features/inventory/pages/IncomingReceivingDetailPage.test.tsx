import { screen } from '@testing-library/react';
import { Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderPage, signInAs } from '../../../test/render';
import { inventoryApi } from '../api';
import { ReceivingCondition, ReceivingRemark, type IncomingReceiving } from '../types';
import { IncomingReceivingDetailPage } from './IncomingReceivingDetailPage';

vi.mock('../api', () => ({
  MOVEMENT_PAGE_SIZE: 30,
  inventoryApi: { listIncomingReceiving: vi.fn(), listPurchaseOrders: vi.fn() },
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
  controlNumber: 'MAIN-IRR-00001',
  createdAt: '2026-10-05T01:00:00Z',
  lines: [
    { id: 'l1', itemId: 'milk', itemName: 'Milk', quantityReceived: 10, uom: 'box', unitPrice: 80, condition: ReceivingCondition.NotGood, remark: ReceivingRemark.Rejected },
  ],
};

beforeEach(() => {
  vi.clearAllMocks();
  signInAs('Manager');
  vi.mocked(inventoryApi.listIncomingReceiving).mockResolvedValue([report]);
  vi.mocked(inventoryApi.listPurchaseOrders).mockResolvedValue([]);
});

describe('IncomingReceivingDetailPage', () => {
  it('shows the report and each of its lines', async () => {
    renderPage(
      <Routes>
        <Route path="/inventory/incoming-receiving/:id" element={<IncomingReceivingDetailPage />} />
      </Routes>,
      { route: '/inventory/incoming-receiving/r1' },
    );
    expect(await screen.findByRole('heading', { name: 'MAIN-IRR-00001' })).toBeInTheDocument();
    expect(screen.getByText('Milk')).toBeInTheDocument();
    expect(screen.getByText('Rejected')).toBeInTheDocument();
    expect(screen.getByText('Not linked')).toBeInTheDocument();
  });
});
