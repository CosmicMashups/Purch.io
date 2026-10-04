import { fireEvent, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderPage, signInAs } from '../../../test/render';
import { dashboardApi } from '../../dashboard/api';
import { InventoryHomePage } from './InventoryHomePage';

vi.mock('../../dashboard/api', () => ({ dashboardApi: { inventory: vi.fn(), sales: vi.fn(), flaggedSync: vi.fn() } }));

describe('InventoryHomePage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(dashboardApi.inventory).mockResolvedValue({
      totalSkus: 64,
      outOfStockCount: 1,
      lowStockCount: 2,
      lowStockItems: [
        { itemId: 'a', itemName: 'Espresso Beans', stockOnHand: 3, lowStockThreshold: 10 },
        { itemId: 'b', itemName: 'Oat Milk', stockOnHand: 0, lowStockThreshold: 12 },
      ],
    });
  });

  it('shows the counts and lists the most urgent item first', async () => {
    signInAs('Warehouse');
    renderPage(<InventoryHomePage />);
    expect(await screen.findByText('64')).toBeInTheDocument();
    const names = (await screen.findAllByRole('listitem')).map((li) => li.textContent).filter((t) => /Beans|Oat/.test(t ?? ''));
    expect(names[0]).toContain('Oat Milk');
    expect(screen.getByText(/Out of stock, alert at 12/)).toBeInTheDocument();
  });

  it('links each alert to a prefilled stock-in', async () => {
    signInAs('Warehouse');
    renderPage(<InventoryHomePage />);
    const links = await screen.findAllByRole('link', { name: 'Add stock' });
    expect(links[0]).toHaveAttribute('href', '/inventory/movements/new?itemId=b&type=0');
  });

  it('hides the catalog tiles from Warehouse but shows them to a Manager', async () => {
    signInAs('Warehouse');
    const view = renderPage(<InventoryHomePage />);
    await screen.findByText('64');
    expect(screen.queryByRole('link', { name: /Categories/ })).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Purchase orders/ })).toBeInTheDocument();
    view.unmount();

    signInAs('Manager');
    renderPage(<InventoryHomePage />);
    expect(await screen.findByRole('link', { name: /Categories/ })).toBeInTheDocument();
  });

  it('shows a retryable error when totals cannot load', async () => {
    signInAs('Manager');
    vi.mocked(dashboardApi.inventory).mockRejectedValue(new Error('boom'));
    renderPage(<InventoryHomePage />);
    expect(await screen.findByText('Stock health is unavailable')).toBeInTheDocument();
  });

  it('has no tabs when ingredients are not tracked separately', async () => {
    signInAs('Warehouse');
    renderPage(<InventoryHomePage />);
    await screen.findByText('64');
    expect(screen.queryByRole('tab', { name: 'Ingredients' })).not.toBeInTheDocument();
  });

  describe('with ingredients tracked separately', () => {
    beforeEach(() => {
      vi.mocked(dashboardApi.inventory).mockResolvedValue({
        totalSkus: 64,
        outOfStockCount: 1,
        lowStockCount: 1,
        lowStockItems: [{ itemId: 'a', itemName: 'Latte', stockOnHand: 3, lowStockThreshold: 10 }],
        ingredients: {
          total: 5,
          outOfStockCount: 0,
          lowStockCount: 1,
          lowStock: [{ inventoryItemId: 'ing1', name: 'Oat Milk', baseUnit: 'ml', quantityOnHand: 800, lowStockThreshold: 2000 }],
        },
      });
    });

    it('splits Stock health into Items and Ingredients, each counted on its own', async () => {
      signInAs('Warehouse');
      renderPage(<InventoryHomePage />);
      await screen.findByText('64');
      fireEvent.click(screen.getAllByRole('tab', { name: 'Ingredients' })[0]);
      expect(await screen.findByText('5')).toBeInTheDocument();
      expect(screen.queryByText('64')).not.toBeInTheDocument();
    });

    it('lists low ingredients on their own tab and sends them to Receive delivery', async () => {
      signInAs('Warehouse');
      renderPage(<InventoryHomePage />);
      expect(await screen.findByText('Latte')).toBeInTheDocument();
      expect(screen.queryByText('Oat Milk')).not.toBeInTheDocument();

      fireEvent.click(screen.getAllByRole('tab', { name: 'Ingredients' })[0]);
      expect(await screen.findByText('Oat Milk')).toBeInTheDocument();
      expect(screen.queryByText('Latte')).not.toBeInTheDocument();
      expect(screen.getByRole('link', { name: 'Receive delivery' })).toHaveAttribute('href', '/inventory/ingredients?receive=ing1');
    });
  });
});
