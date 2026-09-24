import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { renderPage, signInAs } from '../../test/render';
import { BusinessPage } from './BusinessPage';

const tileNames = () => screen.queryAllByRole('link').map((l) => l.textContent ?? '');

describe('BusinessPage tiles', () => {
  it('shows an admin everything, including devices and business settings', () => {
    signInAs('Admin');
    renderPage(<BusinessPage />);
    const text = tileNames().join(' | ');
    for (const label of ['Items', 'Reports', 'Customers', 'Staff', 'Branches', 'Devices', 'Business settings', 'Audit log', 'Promotions']) {
      expect(text).toContain(label);
    }
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
