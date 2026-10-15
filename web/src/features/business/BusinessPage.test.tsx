import { screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { renderPage, signInAs } from '../../test/render';
import { tenantApi } from '../tenant/api';
import type { TenantSettings } from '../tenant/types';
import { BusinessPage } from './BusinessPage';

vi.mock('../tenant/api', () => ({ tenantApi: { get: vi.fn() } }));

const tileNames = () => screen.queryAllByRole('link').map((l) => l.textContent ?? '');

describe('BusinessPage tiles', () => {
  it('shows an admin everything, including devices and business settings', async () => {
    vi.mocked(tenantApi.get).mockResolvedValue({ creditLedgerEnabled: true } as TenantSettings);
    signInAs('Admin');
    renderPage(<BusinessPage />);
    await screen.findByRole('link', { name: /Customers/ });
    const text = tileNames().join(' | ');
    for (const label of ['Modifier groups', 'Customers', 'Staff', 'Branches', 'Devices', 'Business settings', 'Audit log', 'Promotions']) {
      expect(text).toContain(label);
    }
  });

  it('hides Customers and Money owed while customer credit is off', async () => {
    vi.mocked(tenantApi.get).mockResolvedValue({ creditLedgerEnabled: false } as TenantSettings);
    signInAs('Admin');
    renderPage(<BusinessPage />);
    await screen.findByRole('link', { name: /Business settings/ });
    expect(tileNames().join(' | ')).not.toContain('Customers');
    expect(screen.queryByText('Money owed')).not.toBeInTheDocument();
  });

  it('keeps devices and business settings from a manager', () => {
    signInAs('Manager');
    renderPage(<BusinessPage />);
    const text = tileNames().join(' | ');
    expect(text).toContain('Staff');
    expect(text).toContain('Audit log');
    expect(text).not.toContain('Devices');
    expect(text).not.toContain('Business settings');
  });

  it('shows nothing to roles that never reach the Business tab', () => {
    signInAs('Cashier');
    renderPage(<BusinessPage />);
    expect(tileNames()).toEqual([]);
  });
});
