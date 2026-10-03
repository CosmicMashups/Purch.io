import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderPage, signInAs } from '../../test/render';
import { branchesApi } from '../branches/api';
import { memberApi, type Invite, type Member } from './memberApi';
import { StaffPage } from './StaffPage';

vi.mock('./memberApi', () => ({ memberApi: { list: vi.fn(), update: vi.fn(), resetLink: vi.fn(), invites: vi.fn(), invite: vi.fn(), cancelInvite: vi.fn() } }));
vi.mock('../branches/api', () => ({ branchesApi: { list: vi.fn() } }));
vi.mock('qrcode', () => ({ default: { toDataURL: vi.fn().mockResolvedValue('data:image/png;base64,AAAA') } }));

const owner: Member = { id: 'm1', name: 'Ana Reyes', email: 'ana@example.com', role: 0, duties: 0, branchIds: [], isActive: true, hasPin: true };
const manager: Member = { id: 'm2', name: 'Maria Lopez', email: 'maria@example.com', role: 1, duties: 0, branchIds: [], isActive: true, hasPin: true };
const cashier: Member = { id: 'm3', name: 'Ben Santos', email: 'ben@example.com', role: 2, duties: 1, branchIds: ['kat'], isActive: true, hasPin: true };
const pending: Invite = { id: 'i1', purpose: 0, name: 'Carla Dizon', email: 'carla@example.com', role: 2, duties: 2, branchIds: ['kat'], expiresAt: '2026-10-06T02:00:00Z' };

const linkFor = (invite: Invite, token = 'tok123') => ({ invite, token });

beforeEach(() => {
  vi.clearAllMocks();
  signInAs('Admin');
  vi.mocked(memberApi.list).mockResolvedValue([owner, manager, cashier]);
  vi.mocked(memberApi.invites).mockResolvedValue([pending]);
  vi.mocked(branchesApi.list).mockResolvedValue([
    { id: 'kat', name: 'Katipunan', address: null },
    { id: 'qc', name: 'Cubao', address: null },
  ]);
});

describe('StaffPage', () => {
  it('lists each person with what they can do and where, and who is still waiting to join', async () => {
    renderPage(<StaffPage />);
    expect(await screen.findByText('Ben Santos')).toBeInTheDocument();
    expect(screen.getByText('Cashier at Katipunan')).toBeInTheDocument();
    expect(screen.getByText('Admin, every branch')).toBeInTheDocument();
    expect(screen.getByText('maria@example.com')).toBeInTheDocument();
    expect(await screen.findByText('Carla Dizon')).toBeInTheDocument();
    expect(screen.getByText(/Warehouse at Katipunan, link expires/)).toBeInTheDocument();
  });

  it('invites a person and shows the single-use link once, with a QR code', async () => {
    vi.mocked(memberApi.invite).mockResolvedValue(linkFor({ ...pending, id: 'i2', name: 'Dan Uy', email: 'dan@example.com' }));
    renderPage(<StaffPage />);
    fireEvent.change(await screen.findByLabelText('Name'), { target: { value: ' Dan Uy ' } });
    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'dan@example.com' } });
    fireEvent.click(screen.getByLabelText('Warehouse'));
    fireEvent.click(screen.getByLabelText('Katipunan'));
    fireEvent.click(screen.getByRole('button', { name: 'Invite' }));

    await waitFor(() => expect(memberApi.invite).toHaveBeenCalledWith({ name: 'Dan Uy', email: 'dan@example.com', role: 2, duties: 3, branchIds: ['kat'] }));
    const dialog = await screen.findByRole('dialog', { name: 'Invitation link' });
    expect(within(dialog).getByTestId('invite-url')).toHaveTextContent('/enrol/tok123');
    expect(await within(dialog).findByAltText('QR code for dan@example.com')).toBeInTheDocument();
    expect(within(dialog).getByText(/No email is sent/)).toBeInTheDocument();
  });

  it('needs a name, a valid email, a duty and a branch before inviting', async () => {
    renderPage(<StaffPage />);
    fireEvent.click(await screen.findByLabelText('Cashier'));
    fireEvent.click(screen.getByRole('button', { name: 'Invite' }));
    expect(await screen.findByText('Enter their name')).toBeInTheDocument();
    expect(screen.getByText('Enter a valid email address')).toBeInTheDocument();
    expect(screen.getByText('Choose at least one duty')).toBeInTheDocument();
    expect(screen.getByText('Choose at least one branch')).toBeInTheDocument();
    expect(memberApi.invite).not.toHaveBeenCalled();
  });

  it('lets an Admin invite a manager, who works every branch so needs no duties or branches', async () => {
    vi.mocked(memberApi.invite).mockResolvedValue(linkFor({ ...pending, role: 1 }));
    renderPage(<StaffPage />);
    fireEvent.change(await screen.findByLabelText('Role'), { target: { value: '1' } });
    expect(screen.queryByLabelText('Warehouse')).not.toBeInTheDocument();
    fireEvent.change(screen.getByLabelText('Name'), { target: { value: 'Eve Tan' } });
    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'eve@example.com' } });
    fireEvent.click(screen.getByRole('button', { name: 'Invite' }));
    await waitFor(() => expect(memberApi.invite).toHaveBeenCalledWith({ name: 'Eve Tan', email: 'eve@example.com', role: 1, duties: 0, branchIds: [] }));
  });

  it('only offers a Manager the staff role and only staff to manage', async () => {
    signInAs('Manager');
    renderPage(<StaffPage />);
    const role = await screen.findByLabelText('Role');
    expect(within(role).getAllByRole('option').map((o) => o.textContent)).toEqual(['Staff']);
    await screen.findByText('Ben Santos');
    // Edit and reset buttons exist for the one staff member only.
    expect(screen.getAllByRole('button', { name: 'Edit' })).toHaveLength(1);
    expect(screen.getAllByRole('button', { name: 'Password reset link' })).toHaveLength(1);
  });

  it('edits a person: duties, branches and active', async () => {
    vi.mocked(memberApi.update).mockResolvedValue({ ...cashier, duties: 3 });
    renderPage(<StaffPage />);
    fireEvent.click((await screen.findAllByRole('button', { name: 'Edit' }))[2]);
    fireEvent.click(await screen.findByLabelText('Warehouse'));
    fireEvent.click(screen.getByLabelText('Cubao'));
    fireEvent.click(screen.getByLabelText('Active'));
    fireEvent.click(screen.getByRole('button', { name: 'Save' }));
    await waitFor(() => expect(memberApi.update).toHaveBeenCalledWith('m3', { role: 2, duties: 3, branchIds: ['kat', 'qc'], isActive: false }));
  });

  it('makes a password reset link without any email', async () => {
    vi.mocked(memberApi.resetLink).mockResolvedValue(linkFor({ ...pending, purpose: 1, name: 'Ben Santos' }, 'reset9'));
    renderPage(<StaffPage />);
    fireEvent.click((await screen.findAllByRole('button', { name: 'Password reset link' }))[2]);
    await waitFor(() => expect(memberApi.resetLink).toHaveBeenCalledWith('m3'));
    const dialog = await screen.findByRole('dialog', { name: 'Password reset link' });
    expect(within(dialog).getByTestId('invite-url')).toHaveTextContent('/enrol/reset9');
  });

  it('asks before cancelling a waiting link', async () => {
    vi.mocked(memberApi.cancelInvite).mockResolvedValue(undefined);
    renderPage(<StaffPage />);
    fireEvent.click(await screen.findByRole('button', { name: 'Cancel link' }));
    const dialog = screen.getByRole('dialog', { name: 'Cancel this link?' });
    expect(memberApi.cancelInvite).not.toHaveBeenCalled();
    fireEvent.click(within(dialog).getByRole('button', { name: 'Cancel link' }));
    await waitFor(() => expect(memberApi.cancelInvite).toHaveBeenCalledWith('i1'));
  });

  it('shows a retryable error instead of an empty list', async () => {
    vi.mocked(memberApi.list).mockRejectedValue(new Error('boom'));
    renderPage(<StaffPage />);
    expect(await screen.findByText('Staff could not be loaded')).toBeInTheDocument();
  });
});
