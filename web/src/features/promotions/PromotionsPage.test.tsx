import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { catalogApi } from '../catalog/api';
import type { Item } from '../catalog/types';
import { promotionsApi } from './api';
import { PromotionsPage } from './PromotionsPage';

vi.mock('../catalog/api', () => ({ catalogApi: { listItems: vi.fn() } }));
vi.mock('./api', () => ({
  promotionsApi: {
    listBogo: vi.fn(),
    createBogo: vi.fn(),
    updateBogo: vi.fn(),
    listCombos: vi.fn(),
    listItemDiscounts: vi.fn(),
    listPromoCodes: vi.fn(),
    createPromoCode: vi.fn(),
  },
}));

const items = [
  { id: 'latte', name: 'Latte' },
  { id: 'cookie', name: 'Cookie' },
] as Item[];

function renderPage(search = '?type=bogo') {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={[`/business/promotions${search}`]}>
        <PromotionsPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('PromotionsPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(catalogApi.listItems).mockResolvedValue(items);
    vi.mocked(promotionsApi.listBogo).mockResolvedValue([]);
    vi.mocked(promotionsApi.listPromoCodes).mockResolvedValue([]);
  });

  it('lists an existing rule in plain words', async () => {
    vi.mocked(promotionsApi.listBogo).mockResolvedValue([
      { id: 'r1', name: 'Latte B1T1', triggerItemId: 'latte', triggerQuantity: 1, freeItemId: 'cookie', freeQuantity: 1, startsAt: null, endsAt: null, isActive: true },
    ]);
    renderPage();
    expect(await screen.findByText('Buy 1 Latte, get 1 Cookie free')).toBeInTheDocument();
    expect(screen.getByText('Always on')).toBeInTheDocument();
  });

  it('sends a valid Buy 1 Take 1 rule to the API', async () => {
    vi.mocked(promotionsApi.createBogo).mockResolvedValue({} as never);
    renderPage();
    await screen.findByText('No Buy 1 Take 1 promotions yet.');

    fireEvent.change(screen.getByLabelText('Name'), { target: { value: 'Coffee deal' } });
    fireEvent.change(screen.getByLabelText('Buy this item'), { target: { value: 'latte' } });
    fireEvent.change(screen.getByLabelText('Get this item free'), { target: { value: 'cookie' } });
    fireEvent.click(screen.getByRole('button', { name: 'Add' }));

    await waitFor(() => expect(promotionsApi.createBogo).toHaveBeenCalledTimes(1));
    expect(promotionsApi.createBogo).toHaveBeenCalledWith({
      name: 'Coffee deal',
      triggerItemId: 'latte',
      triggerQuantity: 1,
      freeItemId: 'cookie',
      freeQuantity: 1,
      startsAt: null,
      endsAt: null,
    });
  });

  it('blocks submit and explains what is missing', async () => {
    renderPage();
    await screen.findByText('No Buy 1 Take 1 promotions yet.');
    fireEvent.click(screen.getByRole('button', { name: 'Add' }));
    expect(await screen.findByText('Enter a name')).toBeInTheDocument();
    expect(screen.getAllByText('Choose an item')).not.toHaveLength(0);
    expect(promotionsApi.createBogo).not.toHaveBeenCalled();
  });

  it('switches tab from the URL and shows promo codes', async () => {
    renderPage('?type=codes');
    expect(await screen.findByText('No promo codes yet.')).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Promo codes' })).toHaveAttribute('aria-selected', 'true');
  });

  it('shows a retryable error when items cannot load', async () => {
    vi.mocked(catalogApi.listItems).mockRejectedValue(new Error('boom'));
    renderPage();
    expect(await screen.findByText('Items could not be loaded')).toBeInTheDocument();
  });
});
