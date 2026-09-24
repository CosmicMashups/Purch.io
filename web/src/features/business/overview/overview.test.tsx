import { screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderPage, signInAs } from '../../../test/render';
import { creditApi } from '../../credit/api';
import type { CreditLedger } from '../../credit/types';
import { dashboardApi } from '../../dashboard/api';
import { deviceApi } from '../deviceApi';
import { staffApi, type StaffMember } from '../staffApi';
import { MoneyOwed } from './MoneyOwed';
import { NeedsAttention } from './NeedsAttention';
import { teamItems } from './team';
import { DevicesPanel, TeamPanel } from './TeamAndDevices';

vi.mock('../../credit/api', () => ({ creditApi: { list: vi.fn(), reminders: vi.fn() } }));
vi.mock('../../dashboard/api', () => ({ dashboardApi: { sales: vi.fn(), inventory: vi.fn(), flaggedSync: vi.fn() } }));
vi.mock('../deviceApi', () => ({ deviceApi: { list: vi.fn() } }));
vi.mock('../staffApi', () => ({ staffApi: { list: vi.fn() } }));

const ledger = (over: Partial<CreditLedger> & { id: string; customerFullName: string }): CreditLedger => ({
  customerPhoneNumber: '',
  customerAddress: null,
  balance: 0,
  creditLimit: 1000,
  dueDate: null,
  isActive: true,
  ...over,
});
const reminder = (over: { id: string; isOverdue: boolean }) => ({ customerFullName: 'X', customerPhoneNumber: '', balance: 100, dueDate: '2026-09-30', ...over });
const staff = (over: Partial<StaffMember>): StaffMember => ({ id: 'x', name: 'A', role: 2, scopeType: 0, scopeId: null, branchId: null, isActive: true, ...over });
const stock = { totalSkus: 10, outOfStockCount: 0, lowStockCount: 0, lowStockItems: [] };

beforeEach(() => {
  vi.clearAllMocks();
  signInAs('Admin');
  vi.mocked(creditApi.list).mockResolvedValue([]);
  vi.mocked(creditApi.reminders).mockResolvedValue([]);
  vi.mocked(dashboardApi.inventory).mockResolvedValue(stock);
  vi.mocked(dashboardApi.flaggedSync).mockResolvedValue([]);
  vi.mocked(deviceApi.list).mockResolvedValue([]);
  vi.mocked(staffApi.list).mockResolvedValue([]);
});

describe('NeedsAttention', () => {
  it('is good news when nothing needs looking at', async () => {
    renderPage(<NeedsAttention />);
    expect(await screen.findByText('Nothing needs attention right now.')).toBeInTheDocument();
  });

  it('lists each thing with words and a link', async () => {
    vi.mocked(creditApi.reminders).mockResolvedValue([reminder({ id: '1', isOverdue: true }), reminder({ id: '2', isOverdue: true }), reminder({ id: '3', isOverdue: false })]);
    vi.mocked(dashboardApi.inventory).mockResolvedValue({ ...stock, outOfStockCount: 1, lowStockCount: 4 });
    vi.mocked(dashboardApi.flaggedSync).mockResolvedValue([{ id: 'a', deviceId: 'd', entityType: 'Sale', entityId: 'e', clientTimestamp: '', reviewedAt: null }]);
    renderPage(<NeedsAttention />);

    expect(await screen.findByText('2 customer accounts are overdue')).toBeInTheDocument();
    expect(screen.getByText('1 account falls due within 7 days')).toBeInTheDocument();
    expect(screen.getByText('1 item is out of stock')).toBeInTheDocument();
    expect(screen.getByText('4 items are running low')).toBeInTheDocument();
    expect(screen.getByText('1 offline record needs review')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /2 customer accounts are overdue/ })).toHaveAttribute('href', '/business/customers');
    expect(screen.getByRole('link', { name: /out of stock/ })).toHaveAttribute('href', '/inventory');
    expect(screen.getByRole('link', { name: /offline record needs review/ })).toHaveAttribute('href', '/business/sync-conflicts');
  });

  it('admits when a check could not be made, and offers to try again', async () => {
    vi.mocked(dashboardApi.inventory).mockRejectedValue(new Error('boom'));
    renderPage(<NeedsAttention />);
    expect(await screen.findByText(/Some checks could not be loaded/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Try again' })).toBeInTheDocument();
  });
});

describe('MoneyOwed', () => {
  it('totals what is owed and shows each balance against its limit', async () => {
    vi.mocked(creditApi.list).mockResolvedValue([
      ledger({ id: '1', customerFullName: 'Aling Nena', balance: 2900, creditLimit: 3000 }),
      ledger({ id: '2', customerFullName: 'Mang Tomas', balance: 500, creditLimit: 2500 }),
      ledger({ id: '3', customerFullName: 'Closed account', balance: 9999, creditLimit: 9999, isActive: false }),
      ledger({ id: '4', customerFullName: 'Paid up', balance: 0 }),
    ]);
    renderPage(<MoneyOwed />);
    expect(await screen.findByText('₱3,400.00')).toBeInTheDocument();
    expect(screen.getByText('owed across 2 accounts')).toBeInTheDocument();
    expect(screen.getByRole('img', { name: 'Aling Nena: ₱2,900.00 of ₱3,000.00 limit' })).toBeInTheDocument();
    expect(screen.queryByText('Closed account')).not.toBeInTheDocument();
    expect(screen.queryByText('Paid up')).not.toBeInTheDocument();
  });

  it('marks an account over its limit in words', async () => {
    vi.mocked(creditApi.list).mockResolvedValue([ledger({ id: '1', customerFullName: 'Over', balance: 1500, creditLimit: 1000 })]);
    renderPage(<MoneyOwed />);
    const balances = await screen.findAllByText('₱1,500.00');
    expect(balances.some((el) => el.classList.contains('text-danger'))).toBe(true);
  });

  it('handles an account with no limit set', async () => {
    vi.mocked(creditApi.list).mockResolvedValue([ledger({ id: '1', customerFullName: 'No cap', balance: 400, creditLimit: 0 })]);
    renderPage(<MoneyOwed />);
    expect(await screen.findByText('no limit set')).toBeInTheDocument();
  });

  it('says so when nobody owes anything', async () => {
    renderPage(<MoneyOwed />);
    expect(await screen.findByText('No customer owes anything right now.')).toBeInTheDocument();
  });

  it('shows the largest six and says there are more', async () => {
    vi.mocked(creditApi.list).mockResolvedValue(Array.from({ length: 8 }, (_, i) => ledger({ id: String(i), customerFullName: `Customer ${i}`, balance: 100 + i })));
    renderPage(<MoneyOwed />);
    expect(await screen.findByText('Showing the 6 largest balances of 8.')).toBeInTheDocument();
  });
});

describe('team', () => {
  it('gives each role its own colour, and shows switched-off people as outlines', () => {
    const items = teamItems([staff({ role: 0 }), staff({ role: 2 }), staff({ role: 2 }), staff({ role: 2, isActive: false })]);
    expect(items.map((i) => [i.label, i.count, Boolean(i.hollow)])).toEqual([
      ['Admin', 1, false],
      ['Cashier', 2, false],
      ['Cashier, switched off', 1, true],
    ]);
    const cashier = items.find((i) => i.label === 'Cashier');
    expect(items.find((i) => i.label === 'Cashier, switched off')?.color).toBe(cashier?.color);
    expect(items[0].color).not.toBe(cashier?.color);
  });

  it('keeps a role colour the same whichever roles are present', () => {
    const alone = teamItems([staff({ role: 3 })])[0].color;
    const among = teamItems([staff({ role: 0 }), staff({ role: 1 }), staff({ role: 3 })]).find((i) => i.label === 'Warehouse')?.color;
    expect(among).toBe(alone);
  });

  it('draws one square per person', async () => {
    vi.mocked(staffApi.list).mockResolvedValue([staff({ id: '1' }), staff({ id: '2', role: 1 })]);
    renderPage(<TeamPanel />);
    expect(await screen.findByRole('img', { name: 'Staff by role: 2 staff' })).toBeInTheDocument();
  });
});

describe('DevicesPanel', () => {
  it('shows when each device last checked in, most recent first', async () => {
    const recent = new Date(Date.now() - 10 * 60_000).toISOString();
    vi.mocked(deviceApi.list).mockResolvedValue([
      { id: 'a', branchId: 'b', pairingCode: 'X', deviceIdentifier: 'Old kiosk', deviceType: 1, lastSeenAt: null },
      { id: 'b', branchId: 'b', pairingCode: 'Y', deviceIdentifier: 'Front counter', deviceType: 0, lastSeenAt: recent },
    ]);
    renderPage(<DevicesPanel />);
    expect(await screen.findByText('10 min ago')).toBeInTheDocument();
    expect(screen.getByText('Never seen')).toBeInTheDocument();
    const names = screen.getAllByRole('listitem').map((li) => li.textContent ?? '');
    expect(names.findIndex((t) => t.includes('Front counter'))).toBeLessThan(names.findIndex((t) => t.includes('Old kiosk')));
  });

  it('says so when nothing is paired', async () => {
    renderPage(<DevicesPanel />);
    await waitFor(() => expect(screen.getByText('No devices are paired yet.')).toBeInTheDocument());
  });
});
