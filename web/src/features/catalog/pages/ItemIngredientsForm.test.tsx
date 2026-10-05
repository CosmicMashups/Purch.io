import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderPage, signInAs } from '../../../test/render';
import { inventoryApi } from '../../inventory/api';
import type { InventoryItem } from '../../inventory/types';
import { tenantApi } from '../../tenant/api';
import type { TenantSettings } from '../../tenant/types';
import { catalogApi } from '../api';
import { PricingType, TingiMode, type Item } from '../types';
import { ItemDialog } from './ItemDialog';

vi.mock('../api', () => ({
  catalogApi: {
    listCategories: vi.fn(),
    listItems: vi.fn(),
    createItem: vi.fn(),
    updateItem: vi.fn(),
    getRecipe: vi.fn(),
    replaceRecipe: vi.fn(),
  },
}));
vi.mock('../../inventory/api', () => ({ MOVEMENT_PAGE_SIZE: 30, inventoryApi: { listInventoryItems: vi.fn() } }));
vi.mock('../../tenant/api', () => ({ tenantApi: { get: vi.fn() } }));

const ingredient = (id: string, name: string, extra: Partial<InventoryItem> = {}): InventoryItem => ({
  id,
  sortOrder: 0,
  name,
  sku: null,
  baseUnit: 'g',
  packagingUnit: 'sack',
  packagingSize: 1000,
  quantityOnHand: 0,
  lowStockThreshold: null,
  isAutoCreatedForItem: false,
  linkedItemId: null,
  isActive: true,
  categoryId: null,
  ...extra,
});

const beans = ingredient('beans', 'Espresso Beans');
const milk = ingredient('milk', 'Oat Milk', { baseUnit: 'ml' });
const retired = ingredient('old', 'Retired Syrup', { isActive: false });
const ownStock = ingredient('own', 'Latte', { linkedItemId: 'latte', isAutoCreatedForItem: true });

const latte: Item = {
  id: 'latte',
  sortOrder: 0,
  name: 'Latte',
  sku: null,
  barcode: null,
  categoryId: null,
  basePrice: 120,
  imageUrl: null,
  pricingType: PricingType.Unit,
  stockOnHand: 0,
  isActive: true,
  departmentId: null,
  tingiMode: TingiMode.None,
  packagedSize: null,
  tingiIncrementStep: null,
  tingiAllowedSizes: [],
  serviceDurationMinutes: null,
  lowStockThreshold: null,
  isOutOfStock: false,
};

function tracking(on: boolean) {
  vi.mocked(tenantApi.get).mockResolvedValue({ useSeparateInventoryTracking: on } as TenantSettings);
}

const onClose = vi.fn();

const options = () => within(screen.getByRole('listbox')).getAllByRole('option');
const option = (name: RegExp) => within(screen.getByRole('listbox')).getByRole('option', { name });
const combobox = () => screen.findByRole('combobox', { name: 'Ingredients' });

describe('ingredient selector on Add Item', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    signInAs('Manager');
    vi.mocked(catalogApi.listCategories).mockResolvedValue([]);
    vi.mocked(inventoryApi.listInventoryItems).mockResolvedValue([beans, milk, retired, ownStock]);
    vi.mocked(catalogApi.createItem).mockResolvedValue({ ...latte, id: 'new-item' });
    vi.mocked(catalogApi.replaceRecipe).mockResolvedValue([]);
  });

  it('is not offered when ingredients are not tracked separately', async () => {
    tracking(false);
    renderPage(<ItemDialog item={null} onClose={onClose} />);
    await screen.findByLabelText('Name');
    await waitFor(() => expect(tenantApi.get).toHaveBeenCalled());
    expect(screen.queryByRole('combobox', { name: 'Ingredients' })).not.toBeInTheDocument();
  });

  it('narrows the list as you type and ticks several ingredients', async () => {
    tracking(true);
    renderPage(<ItemDialog item={null} onClose={onClose} />);
    const input = await combobox();

    fireEvent.focus(input);
    // Retired ingredients are not offered for a new item.
    await screen.findByRole('listbox');
    expect(options()).toHaveLength(3);
    expect(within(screen.getByRole('listbox')).queryByRole('option', { name: /Retired Syrup/ })).not.toBeInTheDocument();

    fireEvent.change(input, { target: { value: 'oat' } });
    expect(options()).toHaveLength(1);
    fireEvent.click(options()[0]);
    expect(option(/Oat Milk/)).toHaveAttribute('aria-selected', 'true');

    fireEvent.change(input, { target: { value: '' } });
    fireEvent.click(option(/Espresso Beans/));

    const chosen = within(screen.getByRole('list', { name: 'Selected ingredients' }));
    expect(chosen.getByText('Oat Milk')).toBeInTheDocument();
    expect(chosen.getByText('Espresso Beans')).toBeInTheDocument();
  });

  it('creates the item, then saves the ingredients with their quantities as its recipe', async () => {
    tracking(true);
    renderPage(<ItemDialog item={null} onClose={onClose} />);
    fireEvent.change(await screen.findByLabelText('Name'), { target: { value: 'Latte' } });
    fireEvent.change(screen.getByLabelText('Base Price'), { target: { value: '120' } });

    const input = await combobox();
    fireEvent.focus(input);
    await screen.findByRole('listbox');
    fireEvent.click(option(/Espresso Beans/));
    fireEvent.click(option(/Oat Milk/));
    // Beans are used up every order; milk stays availability-only.
    const beansCard = within(screen.getByRole('list', { name: 'Selected ingredients' })).getByText('Espresso Beans').closest('li')!;
    fireEvent.click(within(beansCard).getByRole('radio', { name: 'Used up every order' }));
    fireEvent.change(screen.getByLabelText('Quantity per order (g)'), { target: { value: '18' } });

    fireEvent.click(screen.getByRole('button', { name: 'Save Item' }));

    await waitFor(() => expect(catalogApi.replaceRecipe).toHaveBeenCalledTimes(1));
    expect(catalogApi.replaceRecipe).toHaveBeenCalledWith('new-item', {
      lines: [
        { inventoryItemId: 'beans', quantityPerOrder: 18 },
        { inventoryItemId: 'milk', quantityPerOrder: null },
      ],
    });
    await waitFor(() => expect(onClose).toHaveBeenCalled());
  });

  it('handles the Burger example: bun and patty consumed, dressing only checked', async () => {
    tracking(true);
    const bun = ingredient('bun', 'Burger bun', { baseUnit: 'pair' });
    const patty = ingredient('patty', 'Burger patty', { baseUnit: 'pc' });
    const dressing = ingredient('dressing', 'Dressing', { baseUnit: 'mL' });
    vi.mocked(inventoryApi.listInventoryItems).mockResolvedValue([bun, patty, dressing]);
    renderPage(<ItemDialog item={null} onClose={onClose} />);
    fireEvent.change(await screen.findByLabelText('Name'), { target: { value: 'Burger McDo' } });
    fireEvent.change(screen.getByLabelText('Base Price'), { target: { value: '99' } });

    fireEvent.focus(await combobox());
    await screen.findByRole('listbox');
    fireEvent.click(option(/Burger bun/));
    fireEvent.click(option(/Burger patty/));
    fireEvent.click(option(/Dressing/));

    const chosen = screen.getByRole('list', { name: 'Selected ingredients' });
    const card = (name: string) => within(chosen).getByText(name).closest('li')!;
    for (const name of ['Burger bun', 'Burger patty']) {
      fireEvent.click(within(card(name)).getByRole('radio', { name: 'Used up every order' }));
    }
    // The unit each quantity is in is shown, so nobody has to guess it.
    fireEvent.change(screen.getByLabelText('Quantity per order (pair)'), { target: { value: '1' } });
    fireEvent.change(screen.getByLabelText('Quantity per order (pc)'), { target: { value: '1' } });
    expect(screen.queryByLabelText('Quantity per order (mL)')).not.toBeInTheDocument();
    expect(within(card('Dressing')).getByText(/Not deducted when sold/)).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Save Item' }));
    await waitFor(() => expect(catalogApi.replaceRecipe).toHaveBeenCalledTimes(1));
    expect(catalogApi.replaceRecipe).toHaveBeenCalledWith('new-item', {
      lines: [
        { inventoryItemId: 'bun', quantityPerOrder: 1 },
        { inventoryItemId: 'patty', quantityPerOrder: 1 },
        { inventoryItemId: 'dressing', quantityPerOrder: null },
      ],
    });
  });

  it('will not save a used-up ingredient with no quantity', async () => {
    tracking(true);
    renderPage(<ItemDialog item={null} onClose={onClose} />);
    fireEvent.change(await screen.findByLabelText('Name'), { target: { value: 'Latte' } });
    fireEvent.change(screen.getByLabelText('Base Price'), { target: { value: '120' } });
    fireEvent.focus(await combobox());
    await screen.findByRole('listbox');
    fireEvent.click(option(/Espresso Beans/));
    fireEvent.click(screen.getByRole('radio', { name: 'Used up every order' }));
    fireEvent.click(screen.getByRole('button', { name: 'Save Item' }));
    expect(await screen.findByRole('alert')).toHaveTextContent(/Enter how much is used per order/);
    expect(catalogApi.createItem).not.toHaveBeenCalled();
  });

  it('refuses a bad quantity before creating anything', async () => {
    tracking(true);
    renderPage(<ItemDialog item={null} onClose={onClose} />);
    fireEvent.change(await screen.findByLabelText('Name'), { target: { value: 'Latte' } });
    fireEvent.change(screen.getByLabelText('Base Price'), { target: { value: '120' } });
    fireEvent.focus(await combobox());
    await screen.findByRole('listbox');
    fireEvent.click(option(/Espresso Beans/));
    fireEvent.click(screen.getByRole('radio', { name: 'Used up every order' }));
    fireEvent.change(screen.getByLabelText('Quantity per order (g)'), { target: { value: '-4' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save Item' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Cannot be negative');
    expect(catalogApi.createItem).not.toHaveBeenCalled();
  });

  it('does not touch recipes when no ingredient is chosen', async () => {
    tracking(true);
    renderPage(<ItemDialog item={null} onClose={onClose} />);
    fireEvent.change(await screen.findByLabelText('Name'), { target: { value: 'Latte' } });
    fireEvent.change(screen.getByLabelText('Base Price'), { target: { value: '120' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save Item' }));
    await waitFor(() => expect(onClose).toHaveBeenCalled());
    expect(catalogApi.replaceRecipe).not.toHaveBeenCalled();
  });
});

describe('ingredient selector on Edit Item', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    signInAs('Manager');
    tracking(true);
    vi.mocked(catalogApi.listCategories).mockResolvedValue([]);
    vi.mocked(catalogApi.listItems).mockResolvedValue([latte]);
    vi.mocked(catalogApi.updateItem).mockResolvedValue(latte);
    vi.mocked(catalogApi.replaceRecipe).mockResolvedValue([]);
    vi.mocked(inventoryApi.listInventoryItems).mockResolvedValue([beans, milk, retired, ownStock]);
  });

  const renderEdit = () => renderPage(<ItemDialog item={latte} onClose={onClose} />);

  it('starts from the saved recipe and leaves it alone when nothing changed', async () => {
    vi.mocked(catalogApi.getRecipe).mockResolvedValue([{ inventoryItemId: 'beans', inventoryItemName: 'Espresso Beans', quantityPerOrder: 18 }]);
    renderEdit();

    const chosen = within(await screen.findByRole('list', { name: 'Selected ingredients' }));
    expect(chosen.getByText('Espresso Beans')).toBeInTheDocument();
    expect(screen.getByLabelText('Quantity per order (g)')).toHaveValue('18');

    fireEvent.click(screen.getByRole('button', { name: 'Save Changes' }));
    await waitFor(() => expect(onClose).toHaveBeenCalled());
    expect(catalogApi.replaceRecipe).not.toHaveBeenCalled();
  });

  it('never offers the item\'s own stock record as one of its ingredients', async () => {
    vi.mocked(catalogApi.getRecipe).mockResolvedValue([]);
    renderEdit();
    fireEvent.focus(await combobox());
    await screen.findByRole('listbox');
    expect(options()).toHaveLength(2);
    expect(within(screen.getByRole('listbox')).queryByRole('option', { name: /Latte/ })).not.toBeInTheDocument();
  });

  it('saves the changed ingredients as the new recipe', async () => {
    vi.mocked(catalogApi.getRecipe).mockResolvedValue([{ inventoryItemId: 'beans', inventoryItemName: 'Espresso Beans', quantityPerOrder: 18 }]);
    renderEdit();
    await screen.findByRole('list', { name: 'Selected ingredients' });

    fireEvent.focus(await combobox());
    await screen.findByRole('listbox');
    fireEvent.click(option(/Oat Milk/));
    fireEvent.click(screen.getByRole('button', { name: 'Save Changes' }));

    await waitFor(() => expect(catalogApi.replaceRecipe).toHaveBeenCalledTimes(1));
    expect(catalogApi.replaceRecipe).toHaveBeenCalledWith('latte', {
      lines: [
        { inventoryItemId: 'beans', quantityPerOrder: 18 },
        { inventoryItemId: 'milk', quantityPerOrder: null },
      ],
    });
  });

  it('saving with every ingredient removed sends an empty recipe, making it its own inventory item again', async () => {
    vi.mocked(catalogApi.getRecipe).mockResolvedValue([{ inventoryItemId: 'beans', inventoryItemName: 'Espresso Beans', quantityPerOrder: 18 }]);
    renderEdit();
    await screen.findByRole('list', { name: 'Selected ingredients' });

    fireEvent.click(screen.getByRole('button', { name: /Remove/ }));
    fireEvent.click(screen.getByRole('button', { name: 'Save Changes' }));

    await waitFor(() => expect(catalogApi.replaceRecipe).toHaveBeenCalledWith('latte', { lines: [] }));
  });
});
