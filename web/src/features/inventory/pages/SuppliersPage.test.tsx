import { fireEvent, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderPage, signInAs } from '../../../test/render';
import { inventoryApi } from '../api';
import { SuppliersPage } from './SuppliersPage';

vi.mock('../api', () => ({ MOVEMENT_PAGE_SIZE: 30, inventoryApi: { listSuppliers: vi.fn(), createSupplier: vi.fn(), updateSupplier: vi.fn() } }));

describe('SuppliersPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    signInAs('Warehouse');
    vi.mocked(inventoryApi.listSuppliers).mockResolvedValue([{ id: 's1', name: 'Metro Foods', contactInfo: '0917 555 0101', isActive: true, specialization: 'Dairy', address: null, tin: null, remarks: null, contacts: [{ contactPerson: 'Ana', modes: ['Call'], numbers: ['0917 555 0101'], emails: [] }] }]);
  });

  it('lists suppliers with their contact info', async () => {
    renderPage(<SuppliersPage />);
    expect(await screen.findByText('Metro Foods')).toBeInTheDocument();
    expect(screen.getByText('0917 555 0101')).toBeInTheDocument();
  });

  it('adds a supplier and sends blank details as null', async () => {
    vi.mocked(inventoryApi.createSupplier).mockResolvedValue({} as never);
    renderPage(<SuppliersPage />);
    await screen.findByText('Metro Foods');
    fireEvent.change(screen.getByLabelText('Name'), { target: { value: 'Puregold Wholesale' } });
    fireEvent.click(screen.getByRole('button', { name: 'Add' }));
    await waitFor(() => expect(inventoryApi.createSupplier).toHaveBeenCalledTimes(1));
    expect(vi.mocked(inventoryApi.createSupplier).mock.calls[0][0]).toEqual({ name: 'Puregold Wholesale', specialization: null, address: null, tin: null, remarks: null, contacts: [] });
  });

  it('adds a supplier with a contact that has several numbers and a mode', async () => {
    vi.mocked(inventoryApi.createSupplier).mockResolvedValue({} as never);
    renderPage(<SuppliersPage />);
    await screen.findByText('Metro Foods');
    fireEvent.change(screen.getByLabelText('Name'), { target: { value: 'Acme' } });
    fireEvent.change(screen.getByLabelText('Contact person'), { target: { value: 'Ben' } });
    fireEvent.click(screen.getByLabelText('Viber'));
    fireEvent.change(screen.getByLabelText('Contact number'), { target: { value: '0917 111 2222' } });
    fireEvent.click(screen.getByRole('button', { name: 'Add another number' }));
    fireEvent.change(screen.getByLabelText('Contact number 2'), { target: { value: '0918 333 4444' } });
    fireEvent.change(screen.getByLabelText('Email address'), { target: { value: 'ben@acme.test' } });
    fireEvent.click(screen.getByRole('button', { name: 'Add' }));
    await waitFor(() => expect(inventoryApi.createSupplier).toHaveBeenCalledTimes(1));
    expect(vi.mocked(inventoryApi.createSupplier).mock.calls[0][0].contacts).toEqual([
      { contactPerson: 'Ben', modes: ['Viber'], numbers: ['0917 111 2222', '0918 333 4444'], emails: ['ben@acme.test'] },
    ]);
  });

  it('edits an existing supplier', async () => {
    vi.mocked(inventoryApi.updateSupplier).mockResolvedValue({} as never);
    renderPage(<SuppliersPage />);
    fireEvent.click(await screen.findByRole('button', { name: 'Edit Metro Foods' }));
    expect(screen.getByLabelText('Name')).toHaveValue('Metro Foods');
    fireEvent.change(screen.getByLabelText('Name'), { target: { value: 'Metro Foods Inc' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));
    await waitFor(() => expect(inventoryApi.updateSupplier).toHaveBeenCalledTimes(1));
    expect(vi.mocked(inventoryApi.updateSupplier).mock.calls[0][0]).toBe('s1');
    expect(vi.mocked(inventoryApi.updateSupplier).mock.calls[0][1]).toMatchObject({ name: 'Metro Foods Inc', specialization: 'Dairy', isActive: true });
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
