import { fireEvent, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderPage, signInAs } from '../../../test/render';
import { branchesApi } from '../../branches/api';
import { catalogApi } from '../../catalog/api';
import type { Item } from '../../catalog/types';
import { inventoryApi } from '../api';
import { RecordMovementPage } from './RecordMovementPage';

vi.mock('../../branches/api', () => ({ branchesApi: { list: vi.fn() } }));
vi.mock('../../catalog/api', () => ({ catalogApi: { listItems: vi.fn() } }));
vi.mock('../api', () => ({ MOVEMENT_PAGE_SIZE: 30, inventoryApi: { recordMovement: vi.fn() } }));

const branches = [
  { id: 'kat', name: 'Katipunan', address: null },
  { id: 'kam', name: 'Kamuning', address: null },
];

describe('RecordMovementPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    signInAs('Warehouse', { scope_type: 'Tenant' });
    vi.mocked(catalogApi.listItems).mockResolvedValue([{ id: 'latte', name: 'Latte' }] as Item[]);
    vi.mocked(branchesApi.list).mockResolvedValue(branches);
    vi.mocked(inventoryApi.recordMovement).mockResolvedValue({} as never);
  });

  it('preselects the item and type from the link', async () => {
    renderPage(<RecordMovementPage />, { route: '/?itemId=latte&type=1' });
    expect(await screen.findByLabelText('Item')).toHaveValue('latte');
    expect(screen.getByLabelText('Movement type')).toHaveValue('1');
  });

  it('sends a stock-in with the chosen branch', async () => {
    renderPage(<RecordMovementPage />, { route: '/?itemId=latte' });
    fireEvent.change(await screen.findByLabelText('Branch'), { target: { value: 'kam' } });
    fireEvent.change(screen.getByLabelText('Quantity'), { target: { value: '12' } });
    fireEvent.click(screen.getByRole('button', { name: 'Record movement' }));

    await waitFor(() => expect(inventoryApi.recordMovement).toHaveBeenCalledTimes(1));
    expect(inventoryApi.recordMovement).toHaveBeenCalledWith({
      itemId: 'latte',
      branchId: 'kam',
      type: 0,
      quantity: 12,
      note: null,
      reasonCategory: null,
      photoUrl: null,
      supplierReference: null,
    });
  });

  it('asks for a reason when the movement is spoilage and blocks the send', async () => {
    renderPage(<RecordMovementPage />, { route: '/?itemId=latte&type=3' });
    fireEvent.change(await screen.findByLabelText('Branch'), { target: { value: 'kat' } });
    fireEvent.change(screen.getByLabelText('Quantity'), { target: { value: '2' } });
    fireEvent.click(screen.getByRole('button', { name: 'Record movement' }));
    expect(await screen.findByText('Say why it spoiled')).toBeInTheDocument();
    expect(inventoryApi.recordMovement).not.toHaveBeenCalled();
  });

  it('never offers Sale as a type to record by hand', async () => {
    renderPage(<RecordMovementPage />);
    const select = await screen.findByLabelText('Movement type');
    expect([...select.querySelectorAll('option')].map((o) => o.textContent)).not.toContain('Sale');
  });

  it('gives a branch-scoped account only its own branch, already chosen', async () => {
    signInAs('Manager', { scope_type: 'Branch', scope_id: 'kam' });
    renderPage(<RecordMovementPage />);
    const branch = await screen.findByLabelText('Branch');
    expect(branch).toHaveValue('kam');
    expect([...branch.querySelectorAll('option')].map((o) => o.textContent)).toEqual(['Choose a branch', 'Kamuning']);
  });
});
