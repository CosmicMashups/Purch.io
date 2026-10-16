import { fireEvent, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../../lib/apiError';
import { renderPage, signInAs } from '../../../test/render';
import { branchesApi } from '../../branches/api';
import { inventoryApi } from '../api';
import type { ImportBatch, ImportPlan } from '../types';
import { ImportPage } from './ImportPage';

vi.mock('../api', () => ({
  MOVEMENT_PAGE_SIZE: 30,
  inventoryApi: { imports: { upload: vi.fn(), get: vi.fn(), saveResolutions: vi.fn(), commit: vi.fn() } },
}));
vi.mock('../../branches/api', () => ({ branchesApi: { list: vi.fn() } }));

const emptyPlan: ImportPlan = { willCreate: [], autoLinks: [], needsDecision: [], willUpdate: [], rowErrors: [], unchangedCount: 0, staleResolutions: [] };
const batch = (plan: Partial<ImportPlan>, over: Partial<ImportBatch> = {}): ImportBatch => ({
  id: 'b1', kind: 0, status: 0, plan: { ...emptyPlan, ...plan }, planHash: 'h1', blocked: false, requiresBranch: false, resolutions: {}, result: null, ...over,
});
const create = { kind: 1, name: 'Latte', lines: [2], type: 0, key: null, openingStock: null };
const decision = { key: 'row:3', lines: [3], reason: 1, name: 'Flour 1kg', nearest: [{ id: 'e1', name: 'Flour 1 kg', distance: 1 }] };
const done = { committed: true, result: { batchId: 'b1', createdCategories: [], createdRecords: [], updatedCount: 1, movements: [], committedAt: 'x' } };

async function upload(role: string, b: ImportBatch) {
  signInAs(role, { scope_type: 'Tenant' });
  vi.mocked(branchesApi.list).mockResolvedValue([{ id: 'br1', name: 'Main', isActive: true }] as never);
  vi.mocked(inventoryApi.imports.upload).mockResolvedValue(b);
  const { container } = renderPage(<ImportPage />, { route: '/inventory/import', path: '/inventory/import' });
  const input = container.querySelector('input[type=file]') as HTMLInputElement;
  fireEvent.change(input, { target: { files: [new File(['a'], 'items.csv', { type: 'text/csv' })] } });
  fireEvent.click(screen.getByRole('button', { name: 'Upload and review' }));
  await screen.findByRole('button', { name: 'Upload again' });
  return { input };
}

describe('ImportPage', () => {
  beforeEach(() => vi.clearAllMocks());

  it('keeps Commit disabled until decisions are resolved and creations confirmed', async () => {
    await upload('Manager', batch({ willCreate: [create], needsDecision: [decision] }));
    expect(screen.getByRole('button', { name: 'Commit import' })).toBeDisabled();
    vi.mocked(inventoryApi.imports.saveResolutions).mockResolvedValue(batch({ willCreate: [create] }, { resolutions: { 'row:3': { choice: 'create' } } }));
    fireEvent.click(screen.getByRole('radio', { name: 'Create new' }));
    fireEvent.click(screen.getByRole('button', { name: 'Save choice' }));
    await waitFor(() => expect(screen.queryByRole('button', { name: 'Save choice' })).toBeNull());
    expect(screen.getByRole('button', { name: 'Commit import' })).toBeDisabled();
    fireEvent.click(screen.getByLabelText('Confirm all'));
    expect(screen.getByRole('button', { name: 'Commit import' })).toBeEnabled();
  });

  it('offers link, create and rename for a near match and saves with the decision key', async () => {
    await upload('Manager', batch({ needsDecision: [decision] }));
    expect(screen.getByText('Existing: Flour 1 kg')).toBeInTheDocument();
    expect(screen.getByRole('radio', { name: 'Link to existing' })).toBeInTheDocument();
    expect(screen.getByRole('radio', { name: 'Rename' })).toBeInTheDocument();
    vi.mocked(inventoryApi.imports.saveResolutions).mockResolvedValue(batch({}));
    fireEvent.click(screen.getByRole('button', { name: 'Save choice' }));
    await waitFor(() =>
      expect(inventoryApi.imports.saveResolutions).toHaveBeenCalledWith('b1', { 'row:3': { choice: 'link', targetId: 'e1', newName: null } }),
    );
  });

  it('on a stale 409 shows the review-again message and refetches', async () => {
    const b = batch({});
    await upload('Manager', b);
    vi.mocked(inventoryApi.imports.commit).mockRejectedValue(new ApiError('conflict', 'stale'));
    vi.mocked(inventoryApi.imports.get).mockResolvedValue(b);
    fireEvent.click(screen.getByRole('button', { name: 'Commit import' }));
    fireEvent.change(await screen.findByLabelText(/PIN/i), { target: { value: '1234' } });
    fireEvent.click(screen.getByRole('button', { name: 'Approve' }));
    expect(await screen.findByText(/changed. Review again/i)).toBeInTheDocument();
    await waitFor(() => expect(inventoryApi.imports.get).toHaveBeenCalledWith('b1'));
  });

  it('keeps creation ticks when a decision is saved and clears the stale note', async () => {
    await upload('Manager', batch({ willCreate: [create], needsDecision: [decision] }));
    fireEvent.click(screen.getByLabelText('Confirm all'));
    vi.mocked(inventoryApi.imports.saveResolutions).mockResolvedValue(batch({ willCreate: [create] }, { resolutions: { 'row:3': { choice: 'create' } } }));
    fireEvent.click(screen.getByRole('radio', { name: 'Create new' }));
    fireEvent.click(screen.getByRole('button', { name: 'Save choice' }));
    await waitFor(() => expect(screen.queryByRole('button', { name: 'Save choice' })).toBeNull());
    expect(screen.getByLabelText('Confirm all')).toBeChecked();
    expect(screen.getByRole('button', { name: 'Commit import' })).toBeEnabled();
  });

  it('clears the stale note after a resolution is saved', async () => {
    await upload('Manager', batch({}));
    vi.mocked(inventoryApi.imports.commit).mockRejectedValue(new ApiError('conflict', 'stale'));
    vi.mocked(inventoryApi.imports.get).mockResolvedValue(batch({ needsDecision: [decision] }));
    fireEvent.click(screen.getByRole('button', { name: 'Commit import' }));
    fireEvent.change(await screen.findByLabelText(/PIN/i), { target: { value: '1234' } });
    fireEvent.click(screen.getByRole('button', { name: 'Approve' }));
    expect(await screen.findByText(/changed. Review again/i)).toBeInTheDocument();
    vi.mocked(inventoryApi.imports.saveResolutions).mockResolvedValue(batch({}));
    fireEvent.click(await screen.findByRole('button', { name: 'Save choice' }));
    await waitFor(() => expect(screen.queryByText(/changed. Review again/i)).toBeNull());
  });

  it('changing the kind drops the batch and issues a new idempotency key', async () => {
    await upload('Manager', batch({}));
    const first = vi.mocked(inventoryApi.imports.upload).mock.calls[0][2];
    fireEvent.change(screen.getByLabelText('What is in the file'), { target: { value: 'stock' } });
    expect(screen.queryByRole('button', { name: 'Commit import' })).toBeNull();
    fireEvent.click(screen.getByRole('button', { name: 'Upload and review' }));
    await waitFor(() => expect(inventoryApi.imports.upload).toHaveBeenCalledTimes(2));
    expect(vi.mocked(inventoryApi.imports.upload).mock.calls[1][2]).not.toBe(first);
    expect(vi.mocked(inventoryApi.imports.upload).mock.calls[1][1]).toBe('stock');
  });

  it('lists row errors with line numbers and blocks commit', async () => {
    await upload('Manager', batch({ rowErrors: [{ line: 7, message: 'Price is not a number' }] }));
    expect(screen.getByText('Line 7: Price is not a number')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Commit import' })).toBeDisabled();
  });

  it('lets Warehouse upload but hides Commit', async () => {
    await upload('Warehouse', batch({ willCreate: [create] }));
    expect(screen.getByText('Latte')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Commit import' })).toBeNull();
  });

  it('keeps the PIN dialog open with the error on a wrong PIN', async () => {
    await upload('Manager', batch({}));
    vi.mocked(inventoryApi.imports.commit).mockRejectedValue(new ApiError('validation', 'bad', { approverPin: ['That PIN was not accepted.'] }));
    fireEvent.click(screen.getByRole('button', { name: 'Commit import' }));
    fireEvent.change(await screen.findByLabelText(/PIN/i), { target: { value: '0000' } });
    fireEvent.click(screen.getByRole('button', { name: 'Approve' }));
    expect(await screen.findByText('That PIN was not accepted.')).toBeInTheDocument();
  });

  it('asks for a branch and sends branchId when required', async () => {
    await upload('Manager', batch({}, { requiresBranch: true }));
    expect(screen.getByRole('button', { name: 'Commit import' })).toBeDisabled();
    const picker = screen.getByLabelText('Record stock at branch');
    await screen.findByRole('option', { name: 'Main' });
    fireEvent.change(picker, { target: { value: 'br1' } });
    vi.mocked(inventoryApi.imports.commit).mockResolvedValue(done as never);
    fireEvent.click(screen.getByRole('button', { name: 'Commit import' }));
    fireEvent.change(await screen.findByLabelText(/PIN/i), { target: { value: '1234' } });
    fireEvent.click(screen.getByRole('button', { name: 'Approve' }));
    await waitFor(() => expect(inventoryApi.imports.commit).toHaveBeenCalledWith('b1', expect.objectContaining({ branchId: 'br1', approverPin: '1234' })));
    expect(await screen.findByText('Import complete')).toBeInTheDocument();
  });

  it('generates one idempotency key per selected file', async () => {
    const { input } = await upload('Manager', batch({}));
    const first = vi.mocked(inventoryApi.imports.upload).mock.calls[0][2];
    fireEvent.click(screen.getByRole('button', { name: 'Upload again' }));
    await waitFor(() => expect(inventoryApi.imports.upload).toHaveBeenCalledTimes(2));
    expect(vi.mocked(inventoryApi.imports.upload).mock.calls[1][2]).toBe(first);
    fireEvent.change(input, { target: { files: [new File(['b'], 'other.csv')] } });
    fireEvent.click(screen.getByRole('button', { name: 'Upload again' }));
    await waitFor(() => expect(inventoryApi.imports.upload).toHaveBeenCalledTimes(3));
    expect(vi.mocked(inventoryApi.imports.upload).mock.calls[2][2]).not.toBe(first);
  });
});
