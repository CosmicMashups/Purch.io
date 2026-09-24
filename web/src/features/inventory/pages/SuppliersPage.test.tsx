import { fireEvent, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderPage, signInAs } from '../../../test/render';
import { inventoryApi } from '../api';
import { SuppliersPage } from './SuppliersPage';

vi.mock('../api', () => ({ MOVEMENT_PAGE_SIZE: 30, inventoryApi: { listSuppliers: vi.fn(), createSupplier: vi.fn() } }));

describe('SuppliersPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    signInAs('Warehouse');
    vi.mocked(inventoryApi.listSuppliers).mockResolvedValue([{ id: 's1', name: 'Metro Foods', contactInfo: '0917 555 0101', isActive: true }]);
  });

  it('lists suppliers with their contact info', async () => {
    renderPage(<SuppliersPage />);
    expect(await screen.findByText('Metro Foods')).toBeInTheDocument();
    expect(screen.getByText('0917 555 0101')).toBeInTheDocument();
  });

  it('adds a supplier and sends blank contact info as null', async () => {
    vi.mocked(inventoryApi.createSupplier).mockResolvedValue({} as never);
    renderPage(<SuppliersPage />);
    await screen.findByText('Metro Foods');
    fireEvent.change(screen.getByLabelText('Name'), { target: { value: 'Puregold Wholesale' } });
    fireEvent.click(screen.getByRole('button', { name: 'Add' }));
    await waitFor(() => expect(inventoryApi.createSupplier).toHaveBeenCalledTimes(1));
    expect(vi.mocked(inventoryApi.createSupplier).mock.calls[0][0]).toEqual({ name: 'Puregold Wholesale', contactInfo: null });
  });

  it('requires a name', async () => {
    renderPage(<SuppliersPage />);
    await screen.findByText('Metro Foods');
    fireEvent.click(screen.getByRole('button', { name: 'Add' }));
    expect(await screen.findByText('Enter the supplier name')).toBeInTheDocument();
    expect(inventoryApi.createSupplier).not.toHaveBeenCalled();
  });

  it('shows a retryable error when the list fails', async () => {
    vi.mocked(inventoryApi.listSuppliers).mockRejectedValue(new Error('boom'));
    renderPage(<SuppliersPage />);
    expect(await screen.findByText('Suppliers could not be loaded')).toBeInTheDocument();
  });
});
