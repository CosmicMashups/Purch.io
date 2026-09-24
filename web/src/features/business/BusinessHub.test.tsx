import { screen, within } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { makeItem } from '../../test/pos';
import { renderPage, signInAs } from '../../test/render';
import { branchesApi } from '../branches/api';
import { catalogApi } from '../catalog/api';
import { creditApi } from '../credit/api';
import { dashboardApi } from '../dashboard/api';
import { BusinessPage } from './BusinessPage';
import { deviceApi } from './deviceApi';
import { staffApi } from './staffApi';

vi.mock('../catalog/api', () => ({ catalogApi: { listItems: vi.fn(), listCategories: vi.fn(), listModifierGroups: vi.fn() } }));
vi.mock('../branches/api', () => ({ branchesApi: { list: vi.fn() } }));
vi.mock('../credit/api', () => ({ creditApi: { list: vi.fn(), reminders: vi.fn() } }));
vi.mock('../dashboard/api', () => ({ dashboardApi: { sales: vi.fn(), inventory: vi.fn(), flaggedSync: vi.fn() } }));
vi.mock('./deviceApi', () => ({ deviceApi: { list: vi.fn() } }));
vi.mock('./staffApi', () => ({ staffApi: { list: vi.fn() } }));

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(catalogApi.listItems).mockResolvedValue([makeItem({ id: 'a', name: 'Latte' }), makeItem({ id: 'b', name: 'Mocha' })]);
  vi.mocked(catalogApi.listCategories).mockResolvedValue([{ id: 'c', name: 'Coffee', sortOrder: 1, imageUrl: null }]);
  vi.mocked(catalogApi.listModifierGroups).mockResolvedValue([]);
  vi.mocked(branchesApi.list).mockResolvedValue([
    { id: 'b1', name: 'Main' },
    { id: 'b2', name: 'Annex' },
  ] as never);
  vi.mocked(creditApi.list).mockResolvedValue([
    { id: 'l1', customerFullName: 'Aling Nena', customerPhoneNumber: '', customerAddress: null, balance: 1250, creditLimit: 3000, dueDate: null, isActive: true },
  ]);
  vi.mocked(creditApi.reminders).mockResolvedValue([]);
  vi.mocked(dashboardApi.inventory).mockResolvedValue({ totalSkus: 2, outOfStockCount: 0, lowStockCount: 0, lowStockItems: [] });
  vi.mocked(dashboardApi.flaggedSync).mockResolvedValue([{ id: 'r', deviceId: 'd', entityType: 'Sale', entityId: 'e', clientTimestamp: '', reviewedAt: null }]);
  vi.mocked(deviceApi.list).mockResolvedValue([{ id: 'd1', branchId: 'b1', pairingCode: 'AB', deviceIdentifier: 'Front counter', deviceType: 0, lastSeenAt: null }]);
  vi.mocked(staffApi.list).mockResolvedValue([
    { id: 's1', name: 'Olive', role: 0, scopeType: 0, scopeId: null, branchId: null, isActive: true },
    { id: 's2', name: 'Ana', role: 2, scopeType: 1, scopeId: 'b1', branchId: 'b1', isActive: true },
    { id: 's3', name: 'Dino', role: 2, scopeType: 1, scopeId: 'b1', branchId: 'b1', isActive: false },
  ]);
});

const link = (name: RegExp) => screen.findByRole('link', { name });

describe('Business page for an admin', () => {
  it('puts the things worth a look first', async () => {
    signInAs('Admin');
    renderPage(<BusinessPage />);
    expect(await screen.findByRole('heading', { name: 'Needs attention' })).toBeInTheDocument();
    expect(await screen.findByText('1 offline record needs review')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Money owed' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Team' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Devices' })).toBeInTheDocument();
  });

  it('shows a live figure beside each link, in words', async () => {
    signInAs('Admin');
    renderPage(<BusinessPage />);
    expect(await link(/^Items.*2 items/)).toHaveAttribute('href', '/catalog/items');
    expect(await link(/^Categories.*1 category/)).toBeInTheDocument();
    expect(await link(/^Modifier groups.*0 groups/)).toBeInTheDocument();
    expect(await link(/^Staff.*2 active, 1 off/)).toBeInTheDocument();
    expect(await link(/^Branches.*2 branches/)).toBeInTheDocument();
    expect(await link(/^Devices.*1 paired/)).toBeInTheDocument();
    expect(await link(/^Customers.*₱1,250\.00 owed/)).toBeInTheDocument();
    expect(await link(/^Sync conflicts.*1 to review/)).toBeInTheDocument();
  });

  it('groups the links by what they are for', async () => {
    signInAs('Admin');
    renderPage(<BusinessPage />);
    for (const name of ['Sell and stock', 'People and places', 'Money', 'Records and settings']) {
      expect(await screen.findByRole('heading', { name })).toBeInTheDocument();
    }
    const money = screen.getByRole('heading', { name: 'Money' }).closest('section') as HTMLElement;
    expect(within(money).getByRole('link', { name: /^Reports/ })).toBeInTheDocument();
  });
});

describe('Business page for a manager', () => {
  it('has no devices panel or link, and never asks for the device list', async () => {
    signInAs('Manager');
    renderPage(<BusinessPage />);
    expect(await screen.findByRole('heading', { name: 'Needs attention' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Devices' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /^Devices/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /^Business settings/ })).not.toBeInTheDocument();
    expect(deviceApi.list).not.toHaveBeenCalled();
  });
});
