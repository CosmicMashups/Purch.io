import { fireEvent, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderPage, signInAs } from '../../test/render';
import { auditApi, type AuditEntry } from './audit';
import { AuditLogPage } from './AuditLogPage';
import { staffApi } from './staffApi';

vi.mock('./audit', async (importActual) => ({ ...(await importActual<typeof import('./audit')>()), auditApi: { list: vi.fn() }, AUDIT_PAGE_SIZE: 2 }));
vi.mock('./staffApi', () => ({ staffApi: { list: vi.fn(), create: vi.fn(), update: vi.fn() } }));

const entry = (id: string, over: Partial<AuditEntry> = {}): AuditEntry => ({
  id,
  actorUserId: 'u1',
  actionType: 0,
  targetEntityType: 'Transaction',
  targetEntityId: `3fa85f64-5717-4562-b3fc-2c963f66af${id}`,
  createdAt: `2026-09-2${id}T02:00:00Z`,
  ...over,
});

beforeEach(() => {
  vi.clearAllMocks();
  signInAs('Manager');
  vi.mocked(staffApi.list).mockResolvedValue([{ id: 'u1', name: 'Mario Cruz', role: 1, scopeType: 0, scopeId: null, branchId: null, isActive: true }]);
});

describe('AuditLogPage', () => {
  it('lists entries with a plain action, who did it and the record involved', async () => {
    vi.mocked(auditApi.list).mockResolvedValue([entry('1', { actionType: 8 }), entry('2', { actorUserId: 'gone', actionType: 6 })]);
    renderPage(<AuditLogPage />);
    expect(await screen.findByText('Staff access changed')).toBeInTheDocument();
    expect(await screen.findByText(/Mario Cruz, Transaction #3fa85f64/)).toBeInTheDocument();
    const items = screen.getAllByRole('listitem').map((li) => li.textContent ?? '');
    expect(items.some((t) => t.includes('Credit limit override') && t.includes('A former staff member, Transaction'))).toBe(true);
  });

  it('pages older entries using the last one as the cursor', async () => {
    vi.mocked(auditApi.list).mockResolvedValueOnce([entry('1'), entry('2')]).mockResolvedValueOnce([entry('3')]);
    renderPage(<AuditLogPage />);
    fireEvent.click(await screen.findByRole('button', { name: 'Load older entries' }));
    await waitFor(() => expect(auditApi.list).toHaveBeenCalledTimes(2));
    expect(vi.mocked(auditApi.list).mock.calls[1][1]).toEqual({ before: '2026-09-22T02:00:00Z', beforeId: '2' });
    expect(screen.queryByRole('button', { name: 'Load older entries' })).not.toBeInTheDocument();
  });

  it('filters by action and by staff member', async () => {
    vi.mocked(auditApi.list).mockResolvedValue([]);
    renderPage(<AuditLogPage />);
    await screen.findByText('Nothing matches these filters.');
    fireEvent.change(screen.getByLabelText('Action'), { target: { value: '1' } });
    await waitFor(() => expect(vi.mocked(auditApi.list).mock.calls.at(-1)![0]).toMatchObject({ actionType: 1 }));
    fireEvent.change(await screen.findByLabelText('Staff member'), { target: { value: 'u1' } });
    await waitFor(() => expect(vi.mocked(auditApi.list).mock.calls.at(-1)![0]).toMatchObject({ actionType: 1, actorUserId: 'u1' }));
  });

  it('sends whole Manila days for date filters and does not fetch a reversed range', async () => {
    vi.mocked(auditApi.list).mockResolvedValue([]);
    renderPage(<AuditLogPage />);
    await screen.findByText('Nothing matches these filters.');
    fireEvent.change(screen.getByLabelText('From'), { target: { value: '2026-09-01' } });
    await waitFor(() => expect(vi.mocked(auditApi.list).mock.calls.at(-1)![0]).toMatchObject({ from: '2026-08-31T16:00:00.000Z' }));

    vi.mocked(auditApi.list).mockClear();
    fireEvent.change(screen.getByLabelText('To'), { target: { value: '2026-08-01' } });
    expect(await screen.findByText('The end date is before the start date')).toBeInTheDocument();
    expect(screen.getByText('Choose a valid date range to see the log.')).toBeInTheDocument();
    expect(auditApi.list).not.toHaveBeenCalled();
  });

  it('shows a retryable error instead of an empty log', async () => {
    vi.mocked(auditApi.list).mockRejectedValue(new Error('boom'));
    renderPage(<AuditLogPage />);
    expect(await screen.findByText('The audit log could not be loaded')).toBeInTheDocument();
    expect(screen.queryByText('Nothing matches these filters.')).not.toBeInTheDocument();
  });
});
