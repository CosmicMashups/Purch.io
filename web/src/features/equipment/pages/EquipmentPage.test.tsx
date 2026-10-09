import { fireEvent, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { chooseFromMenu, menuItemNames } from '../../../test/menu';
import { renderPage, signInAs } from '../../../test/render';
import { useToastStore } from '../../../components/feedback/toastStore';
import { equipmentApi } from '../api';
import { EquipmentKind, EquipmentStatus, type Equipment } from '../types';
import { EquipmentPage } from './EquipmentPage';

vi.mock('../api', () => ({
  equipmentApi: {
    list: vi.fn(),
    create: vi.fn(),
    update: vi.fn(),
    setStatus: vi.fn(),
    reorder: vi.fn(),
    getItemEquipment: vi.fn(),
    replaceItemEquipment: vi.fn(),
  },
}));

const machine: Equipment = {
  id: 'machine',
  name: 'Ice cream machine',
  kind: EquipmentKind.Equipment,
  status: EquipmentStatus.Operational,
  quantity: null,
  location: 'Kitchen',
  notes: null,
  isActive: true,
  sortOrder: 0,
  usedByItemCount: 2,
};

const spoons: Equipment = { ...machine, id: 'spoons', name: 'Spoons', kind: EquipmentKind.Utensil, quantity: 40, location: null, usedByItemCount: 0, sortOrder: 1 };

describe('EquipmentPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    useToastStore.setState({ toasts: [] });
    signInAs('Warehouse', { scope_type: 'Branch', scope_id: 'kat' });
    vi.mocked(equipmentApi.list).mockResolvedValue([machine, spoons]);
  });

  it('lists equipment under its type with status, quantity and how many items need it', async () => {
    renderPage(<EquipmentPage />);
    expect(await screen.findByText('Ice cream machine')).toBeInTheDocument();
    expect(screen.getAllByText('Operational')).toHaveLength(2);
    expect(screen.getByText('2 items')).toBeInTheDocument();
    expect(screen.getByText('Utensils')).toBeInTheDocument();
    expect(screen.getByText('40')).toBeInTheDocument();
  });

  it('adds equipment, turning blank optional fields into null', async () => {
    vi.mocked(equipmentApi.create).mockResolvedValue(machine);
    renderPage(<EquipmentPage />);
    fireEvent.click(await screen.findByRole('button', { name: 'Add Equipment' }));
    fireEvent.change(await screen.findByLabelText('Name'), { target: { value: 'Deep fryer' } });
    fireEvent.click(screen.getByRole('button', { name: 'Add' }));

    await waitFor(() => expect(equipmentApi.create).toHaveBeenCalledTimes(1));
    expect(equipmentApi.create).toHaveBeenCalledWith({ name: 'Deep fryer', kind: EquipmentKind.Equipment, quantity: null, location: null, notes: null });
  });

  it('refuses a negative or fractional quantity', async () => {
    renderPage(<EquipmentPage />);
    fireEvent.click(await screen.findByRole('button', { name: 'Add Equipment' }));
    fireEvent.change(await screen.findByLabelText('Name'), { target: { value: 'Chairs' } });
    fireEvent.change(screen.getByLabelText(/Quantity/), { target: { value: '2.5' } });
    fireEvent.click(screen.getByRole('button', { name: 'Add' }));

    expect(await screen.findByText('Enter a whole number')).toBeInTheDocument();
    expect(equipmentApi.create).not.toHaveBeenCalled();
  });

  it('edits equipment with its values preloaded', async () => {
    vi.mocked(equipmentApi.update).mockResolvedValue(machine);
    renderPage(<EquipmentPage />);
    await chooseFromMenu('Ice cream machine', 'Edit');
    expect(screen.getByLabelText('Name')).toHaveValue('Ice cream machine');
    expect(screen.getByLabelText('Location (optional)')).toHaveValue('Kitchen');
    fireEvent.change(screen.getByLabelText('Location (optional)'), { target: { value: 'Front counter' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));

    await waitFor(() => expect(equipmentApi.update).toHaveBeenCalledTimes(1));
    expect(equipmentApi.update).toHaveBeenCalledWith('machine', expect.objectContaining({ location: 'Front counter', isActive: true }));
  });

  it('marks equipment out of service from the row menu', async () => {
    vi.mocked(equipmentApi.setStatus).mockResolvedValue({ ...machine, status: EquipmentStatus.OutOfService });
    renderPage(<EquipmentPage />);
    await chooseFromMenu('Ice cream machine', 'Mark out of service');

    await waitFor(() => expect(equipmentApi.setStatus).toHaveBeenCalledWith('machine', EquipmentStatus.OutOfService));
    await waitFor(() => expect(useToastStore.getState().toasts[0]?.message).toMatch(/2 items show as out of stock/));
  });

  it('offers every status except the current one', async () => {
    vi.mocked(equipmentApi.list).mockResolvedValue([{ ...machine, status: EquipmentStatus.OutOfService }]);
    renderPage(<EquipmentPage />);
    await screen.findByText('Out of service');
    const names = await menuItemNames('Ice cream machine');

    expect(names.some((n) => n.startsWith('Mark operational'))).toBe(true);
    expect(names.some((n) => n.startsWith('Mark needs repair'))).toBe(true);
    expect(names.some((n) => n.startsWith('Mark out of service'))).toBe(false);
  });

  it('hides inactive equipment until the Inactive filter is chosen', async () => {
    vi.mocked(equipmentApi.list).mockResolvedValue([machine, { ...spoons, isActive: false }]);
    renderPage(<EquipmentPage />);
    await screen.findByText('Ice cream machine');
    expect(screen.queryByText('Spoons')).not.toBeInTheDocument();
  });
});
