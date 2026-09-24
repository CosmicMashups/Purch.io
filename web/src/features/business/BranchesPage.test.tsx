import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderPage, signInAs } from '../../test/render';
import { branchAdminApi } from '../branches/adminApi';
import { branchesApi } from '../branches/api';
import type { Branch } from '../branches/types';
import { BranchesPage } from './BranchesPage';

vi.mock('../branches/api', () => ({ branchesApi: { list: vi.fn() } }));
vi.mock('../branches/adminApi', () => ({
  branchAdminApi: { create: vi.fn(), updateHardware: vi.fn(), updateGcash: vi.fn(), listDepartments: vi.fn(), createDepartment: vi.fn() },
}));
vi.mock('../uploads/api', () => ({ uploadsApi: { uploadImage: vi.fn() } }));
vi.mock('../departments/api', () => ({ departmentsApi: { listAllDepartments: vi.fn() } }));

const kat: Branch = {
  id: 'kat',
  name: 'Katipunan',
  address: 'QC',
  receiptPrinterProfile: 0,
  cashDrawerEnabled: false,
  cashDrawerPolicy: 0,
  manualGcashQrImageUrl: null,
  manualGcashAccountName: 'Kape Katipunan',
  manualGcashAccountNumber: null,
};

beforeEach(() => {
  vi.clearAllMocks();
  signInAs('Admin');
  vi.mocked(branchesApi.list).mockResolvedValue([kat, { id: 'kam', name: 'Kamuning', address: null }]);
  vi.mocked(branchAdminApi.listDepartments).mockResolvedValue([{ id: 'd1', branchId: 'kat', name: 'Bakery', concessionaireContactInfo: '0917 555 0101' }]);
  vi.mocked(branchAdminApi.create).mockResolvedValue(kat);
  vi.mocked(branchAdminApi.updateHardware).mockResolvedValue(kat);
  vi.mocked(branchAdminApi.updateGcash).mockResolvedValue(kat);
  vi.mocked(branchAdminApi.createDepartment).mockResolvedValue({ id: 'd2', branchId: 'kat', name: 'Grill', concessionaireContactInfo: null });
});

describe('BranchesPage', () => {
  it('lets a manager see branches but not change them', async () => {
    signInAs('Manager');
    renderPage(<BranchesPage />);
    expect(await screen.findByText('Katipunan')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Manage' })).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Name')).not.toBeInTheDocument();
  });

  it('adds a branch, sending a blank address as null', async () => {
    renderPage(<BranchesPage />);
    fireEvent.change(await screen.findByLabelText('Name'), { target: { value: 'Timog' } });
    fireEvent.click(screen.getByRole('button', { name: 'Add' }));
    await waitFor(() => expect(branchAdminApi.create).toHaveBeenCalledWith({ name: 'Timog', address: null }));
  });

  it('needs a branch name', async () => {
    renderPage(<BranchesPage />);
    await screen.findByText('Katipunan');
    fireEvent.click(screen.getByRole('button', { name: 'Add' }));
    expect(await screen.findByText('Enter the branch name')).toBeInTheDocument();
    expect(branchAdminApi.create).not.toHaveBeenCalled();
  });

  it('shows a retryable error rather than an empty list', async () => {
    vi.mocked(branchesApi.list).mockRejectedValue(new Error('boom'));
    renderPage(<BranchesPage />);
    expect(await screen.findByText('Branches could not be loaded')).toBeInTheDocument();
  });
});

describe('managing one branch', () => {
  async function open() {
    renderPage(<BranchesPage />);
    fireEvent.click((await screen.findAllByRole('button', { name: 'Manage' }))[0]);
  }

  it('saves hardware settings as the API expects them', async () => {
    await open();
    const form = await screen.findByRole('form', { name: 'Hardware settings' });
    expect(within(form).getByLabelText('When the drawer opens')).toBeDisabled();

    fireEvent.change(within(form).getByLabelText('Receipt printer'), { target: { value: '1' } });
    fireEvent.click(within(form).getByLabelText('This branch has a cash drawer'));
    expect(within(form).getByLabelText('When the drawer opens')).toBeEnabled();
    fireEvent.change(within(form).getByLabelText('When the drawer opens'), { target: { value: '1' } });
    fireEvent.click(within(form).getByRole('button', { name: 'Save hardware settings' }));

    await waitFor(() => expect(branchAdminApi.updateHardware).toHaveBeenCalledWith('kat', { receiptPrinterProfile: 1, cashDrawerEnabled: true, cashDrawerPolicy: 1 }));
  });

  it('saves the GCash details, sending blanks as null', async () => {
    await open();
    const form = await screen.findByRole('form', { name: 'Manual GCash QR' });
    expect(within(form).getByLabelText('Account name')).toHaveValue('Kape Katipunan');
    fireEvent.change(within(form).getByLabelText('Account number'), { target: { value: ' 0917 555 0101 ' } });
    fireEvent.click(within(form).getByRole('button', { name: 'Save GCash QR' }));
    await waitFor(() => expect(branchAdminApi.updateGcash).toHaveBeenCalledWith('kat', { qrImageUrl: null, accountName: 'Kape Katipunan', accountNumber: '0917 555 0101' }));
  });

  it('lists departments and adds one', async () => {
    await open();
    const section = await screen.findByRole('region', { name: 'Departments' });
    expect(await within(section).findByText('Bakery')).toBeInTheDocument();
    fireEvent.change(within(section).getByLabelText('New department'), { target: { value: 'Grill' } });
    fireEvent.click(within(section).getByRole('button', { name: 'Add department' }));
    await waitFor(() => expect(branchAdminApi.createDepartment).toHaveBeenCalledWith('kat', { name: 'Grill', concessionaireContactInfo: null }));
  });

  it('needs a department name', async () => {
    await open();
    const section = await screen.findByRole('region', { name: 'Departments' });
    fireEvent.click(within(section).getByRole('button', { name: 'Add department' }));
    expect(await within(section).findByText('Enter the department name')).toBeInTheDocument();
    expect(branchAdminApi.createDepartment).not.toHaveBeenCalled();
  });

  it('closes back to the new-branch form', async () => {
    await open();
    fireEvent.click(await screen.findByRole('button', { name: 'Close' }));
    expect(await screen.findByLabelText('Name')).toBeInTheDocument();
  });
});
