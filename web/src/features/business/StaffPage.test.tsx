import { fireEvent, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { useToastStore } from '../../components/feedback/toastStore';
import { ApiError } from '../../lib/apiError';
import { renderPage, signInAs } from '../../test/render';
import { branchesApi } from '../branches/api';
import { departmentsApi } from '../departments/api';
import { staffApi, type StaffMember } from './staffApi';
import { StaffPage } from './StaffPage';

vi.mock('./staffApi', () => ({ staffApi: { list: vi.fn(), create: vi.fn(), update: vi.fn() } }));
vi.mock('../branches/api', () => ({ branchesApi: { list: vi.fn() } }));
vi.mock('../departments/api', () => ({ departmentsApi: { listAllDepartments: vi.fn() } }));

const members: StaffMember[] = [
  { id: 'u1', name: 'Mario Cruz', role: 0, scopeType: 0, scopeId: null, branchId: null, isActive: true },
  { id: 'u2', name: 'Ana Reyes', role: 2, scopeType: 1, scopeId: 'kat', branchId: 'kat', isActive: true },
  { id: 'u3', name: 'Old Timer', role: 3, scopeType: 0, scopeId: null, branchId: null, isActive: false },
];

beforeEach(() => {
  vi.clearAllMocks();
  useToastStore.setState({ toasts: [] });
  signInAs('Admin');
  vi.mocked(staffApi.list).mockResolvedValue(members);
  vi.mocked(branchesApi.list).mockResolvedValue([
    { id: 'kat', name: 'Katipunan', address: null },
    { id: 'kam', name: 'Kamuning', address: null },
  ]);
  vi.mocked(departmentsApi.listAllDepartments).mockResolvedValue([{ id: 'grill', name: 'Grill', branchId: 'kam' }]);
  vi.mocked(staffApi.create).mockResolvedValue(members[1]);
  vi.mocked(staffApi.update).mockResolvedValue(members[1]);
});

describe('StaffPage list', () => {
  it('shows each person with their role and where they can work', async () => {
    renderPage(<StaffPage />);
    expect(await screen.findByText('Mario Cruz')).toBeInTheDocument();
    expect(screen.getByText('Admin, Whole business')).toBeInTheDocument();
    expect(screen.getByText('Cashier, Katipunan')).toBeInTheDocument();
    expect(screen.getByText('Inactive')).toBeInTheDocument();
  });

  it('lets a manager look but not change', async () => {
    signInAs('Manager');
    renderPage(<StaffPage />);
    expect(await screen.findByText('Mario Cruz')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Edit' })).not.toBeInTheDocument();
    expect(screen.queryByLabelText('PIN')).not.toBeInTheDocument();
    expect(screen.getByText('Only an admin can add or change staff.')).toBeInTheDocument();
  });

  it('shows a retryable error instead of an empty list', async () => {
    vi.mocked(staffApi.list).mockRejectedValue(new Error('boom'));
    renderPage(<StaffPage />);
    expect(await screen.findByText('Staff could not be loaded')).toBeInTheDocument();
  });
});

describe('adding staff', () => {
  async function fill(name: string, pin: string) {
    fireEvent.change(await screen.findByLabelText('Name'), { target: { value: name } });
    fireEvent.change(screen.getByLabelText('PIN'), { target: { value: pin } });
  }

  it('adds a whole-business cashier with no scope id or branch', async () => {
    renderPage(<StaffPage />);
    await fill('Ben Cruz', ' 4321 ');
    fireEvent.click(screen.getByRole('button', { name: 'Add' }));
    await waitFor(() => expect(staffApi.create).toHaveBeenCalledTimes(1));
    expect(staffApi.create).toHaveBeenCalledWith({ name: 'Ben Cruz', role: 2, scopeType: 0, scopeId: null, branchId: null, pin: '4321' });
  });

  it('gives a branch-limited person their branch as scope and branch together', async () => {
    renderPage(<StaffPage />);
    await fill('Ben Cruz', '4321');
    fireEvent.change(screen.getByLabelText('Can work in'), { target: { value: '1' } });
    fireEvent.change(await screen.findByLabelText('Branch'), { target: { value: 'kam' } });
    fireEvent.click(screen.getByRole('button', { name: 'Add' }));
    await waitFor(() => expect(staffApi.create).toHaveBeenCalledWith({ name: 'Ben Cruz', role: 2, scopeType: 1, scopeId: 'kam', branchId: 'kam', pin: '4321' }));
  });

  it("gives a department-limited person the department and that department's branch", async () => {
    renderPage(<StaffPage />);
    await fill('Ben Cruz', '4321');
    fireEvent.change(screen.getByLabelText('Can work in'), { target: { value: '2' } });
    fireEvent.change(await screen.findByLabelText('Department'), { target: { value: 'grill' } });
    fireEvent.click(screen.getByRole('button', { name: 'Add' }));
    await waitFor(() => expect(staffApi.create).toHaveBeenCalledWith({ name: 'Ben Cruz', role: 2, scopeType: 2, scopeId: 'grill', branchId: 'kam', pin: '4321' }));
  });

  it('asks for a branch when one-branch access is chosen without one', async () => {
    renderPage(<StaffPage />);
    await fill('Ben Cruz', '4321');
    fireEvent.change(screen.getByLabelText('Can work in'), { target: { value: '1' } });
    fireEvent.click(screen.getByRole('button', { name: 'Add' }));
    expect(await screen.findByText('Choose a branch', { selector: 'p' })).toBeInTheDocument();
    expect(staffApi.create).not.toHaveBeenCalled();
  });

  it('rejects a short or non-numeric PIN before calling the API', async () => {
    renderPage(<StaffPage />);
    await fill('Ben Cruz', '12a');
    fireEvent.click(screen.getByRole('button', { name: 'Add' }));
    expect(await screen.findByText('Use 4 to 8 digits')).toBeInTheDocument();
    expect(staffApi.create).not.toHaveBeenCalled();
  });

  it('needs a name', async () => {
    renderPage(<StaffPage />);
    fireEvent.change(await screen.findByLabelText('PIN'), { target: { value: '4321' } });
    fireEvent.click(screen.getByRole('button', { name: 'Add' }));
    expect(await screen.findByText('Enter a name')).toBeInTheDocument();
  });

  it('locks an admin to the whole business', async () => {
    renderPage(<StaffPage />);
    fireEvent.change(await screen.findByLabelText('Role'), { target: { value: '0' } });
    const scope = screen.getByLabelText('Can work in');
    expect([...scope.querySelectorAll('option')].map((o) => o.textContent)).toEqual(['Whole business']);
  });

  it('shows the API message when a PIN is already taken', async () => {
    vi.mocked(staffApi.create).mockRejectedValue(new ApiError('validation', 'That PIN is already in use by another staff member. Choose a different one.'));
    renderPage(<StaffPage />);
    await fill('Ben Cruz', '4321');
    fireEvent.click(screen.getByRole('button', { name: 'Add' }));
    await waitFor(() => expect(useToastStore.getState().toasts[0]?.message).toMatch(/already in use/));
  });
});

describe('editing staff', () => {
  it('shows the person without a PIN field, and saves a role and access change', async () => {
    renderPage(<StaffPage />);
    fireEvent.click((await screen.findAllByRole('button', { name: 'Edit' }))[1]);

    expect(screen.queryByLabelText('PIN')).not.toBeInTheDocument();
    expect(screen.getByLabelText('Role')).toHaveValue('2');
    expect(screen.getByLabelText('Branch')).toHaveValue('kat');

    fireEvent.change(screen.getByLabelText('Role'), { target: { value: '1' } });
    fireEvent.change(screen.getByLabelText('Can work in'), { target: { value: '0' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));
    await waitFor(() => expect(staffApi.update).toHaveBeenCalledWith('u2', { role: 1, scopeType: 0, scopeId: null, branchId: null, isActive: true }));
  });

  it('can deactivate someone', async () => {
    renderPage(<StaffPage />);
    fireEvent.click((await screen.findAllByRole('button', { name: 'Edit' }))[1]);
    fireEvent.click(screen.getByLabelText('Active'));
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));
    await waitFor(() => expect(vi.mocked(staffApi.update).mock.calls[0][1]).toMatchObject({ isActive: false }));
  });
});
