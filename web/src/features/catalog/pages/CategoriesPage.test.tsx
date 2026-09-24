import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { catalogApi } from '../api';
import { CategoriesPage } from './CategoriesPage';

vi.mock('../api', () => ({ catalogApi: { listCategories: vi.fn(), createCategory: vi.fn(), updateCategory: vi.fn() } }));

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <CategoriesPage />
    </QueryClientProvider>,
  );
}

describe('CategoriesPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(catalogApi.listCategories).mockResolvedValue([
      { id: 'b', name: 'Pastries', sortOrder: 2, imageUrl: null },
      { id: 'a', name: 'Coffee', sortOrder: 1, imageUrl: null },
    ]);
  });

  it('lists categories in sort order', async () => {
    renderPage();
    const rows = await screen.findAllByRole('listitem');
    expect(rows[0]).toHaveTextContent('Coffee');
    expect(rows[1]).toHaveTextContent('Pastries');
  });

  it('creates a category', async () => {
    vi.mocked(catalogApi.createCategory).mockResolvedValue({ id: 'c', name: 'Drinks', sortOrder: 3, imageUrl: null });
    renderPage();
    await screen.findByText('Coffee');
    fireEvent.change(screen.getByLabelText('Name'), { target: { value: 'Drinks' } });
    fireEvent.change(screen.getByLabelText('Sort order'), { target: { value: '3' } });
    fireEvent.click(screen.getByRole('button', { name: 'Add Category' }));
    await waitFor(() => expect(catalogApi.createCategory).toHaveBeenCalledTimes(1));
    expect(catalogApi.createCategory).toHaveBeenCalledWith({ name: 'Drinks', sortOrder: 3, imageUrl: null });
  });

  it('will not submit a blank name', async () => {
    renderPage();
    await screen.findByText('Coffee');
    fireEvent.click(screen.getByRole('button', { name: 'Add Category' }));
    expect(await screen.findByText('Required')).toBeInTheDocument();
    expect(catalogApi.createCategory).not.toHaveBeenCalled();
  });

  it('shows a retryable error instead of an empty list when loading fails', async () => {
    vi.mocked(catalogApi.listCategories).mockRejectedValue(new Error('boom'));
    renderPage();
    expect(await screen.findByRole('button', { name: 'Try again' })).toBeInTheDocument();
    expect(screen.queryByText('No categories yet')).not.toBeInTheDocument();
  });
});
