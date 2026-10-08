import { fireEvent, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../lib/apiError';
import { useAuthStore } from '../../lib/authStore';
import { renderPage } from '../../test/render';
import { EnrolPage, enrolApi } from './EnrolPage';

const base = { businessName: "Ana's Store", name: 'Ben Santos', email: 'ben@example.com', purpose: 0, role: 2, duties: 1, hasAccount: false };

beforeEach(() => {
  vi.restoreAllMocks();
  useAuthStore.setState({ accessToken: null, refreshToken: null });
});

function renderEnrol() {
  return renderPage(<EnrolPage />, { route: '/enrol/tok', path: '/enrol/:token', otherRoutes: [{ path: '/', element: <p>Home</p> }] });
}

function fill(label: string, value: string) {
  fireEvent.change(screen.getByLabelText(new RegExp(`^${label}`)), { target: { value } });
}

describe('EnrolPage', () => {
  it('asks an owner from the old PIN-only sign-in for their email, then signs them in', async () => {
    vi.spyOn(enrolApi, 'preview').mockResolvedValue({ ...base, name: 'Old Owner', email: '', role: 0, duties: 0 });
    const redeem = vi.spyOn(enrolApi, 'redeem').mockResolvedValue({ accessToken: 'a9', refreshToken: 'r9' });
    renderEnrol();
    await screen.findByRole('heading', { name: "Join Ana's Store" });

    fill('Your email', 'owner@example.com');
    fill('Choose a password', 'long enough pass');
    fill('Type it again', 'long enough pass');
    fill('Choose a PIN', '482112');
    fireEvent.click(screen.getByRole('button', { name: 'Join' }));

    expect(await screen.findByText('Home')).toBeInTheDocument();
    expect(redeem).toHaveBeenCalledWith('tok', 'long enough pass', '482112', 'owner@example.com');
  });

  it('says plainly when the link cannot be used', async () => {
    vi.spyOn(enrolApi, 'preview').mockRejectedValue(new ApiError('notFound', 'raw'));
    renderEnrol();
    expect(await screen.findByRole('heading', { name: 'This link cannot be used' })).toBeInTheDocument();
  });

  it('sets a password and a PIN for a new person, then signs them in', async () => {
    vi.spyOn(enrolApi, 'preview').mockResolvedValue(base);
    const redeem = vi.spyOn(enrolApi, 'redeem').mockResolvedValue({ accessToken: 'a1', refreshToken: 'r1' });
    renderEnrol();
    expect(await screen.findByRole('heading', { name: "Join Ana's Store" })).toBeInTheDocument();
    expect(screen.getByText(/Staff, Cashier/)).toBeInTheDocument();

    fill('Choose a password', 'long enough pass');
    fill('Type it again', 'long enough pass');
    fill('Choose a PIN', '482112');
    fireEvent.click(screen.getByRole('button', { name: 'Join' }));

    expect(await screen.findByText('Home')).toBeInTheDocument();
    expect(redeem).toHaveBeenCalledWith('tok', 'long enough pass', '482112', null);
    expect(useAuthStore.getState().accessToken).toBe('a1');
  });

  it('checks the password, the repeat and the PIN before sending anything', async () => {
    vi.spyOn(enrolApi, 'preview').mockResolvedValue(base);
    const redeem = vi.spyOn(enrolApi, 'redeem');
    renderEnrol();
    await screen.findByRole('heading', { name: "Join Ana's Store" });

    fill('Choose a password', 'short');
    fireEvent.click(screen.getByRole('button', { name: 'Join' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('at least 8 characters');

    fill('Choose a password', 'long enough pass');
    fill('Type it again', 'different pass here');
    fireEvent.click(screen.getByRole('button', { name: 'Join' }));
    expect(await screen.findByText('The two passwords do not match.')).toBeInTheDocument();

    fill('Type it again', 'long enough pass');
    fill('Choose a PIN', '12');
    fireEvent.click(screen.getByRole('button', { name: 'Join' }));
    expect(await screen.findByText('Choose a PIN of 6 to 8 digits.')).toBeInTheDocument();
    expect(redeem).not.toHaveBeenCalled();
  });

  it('asks for the existing password from someone who already has an account', async () => {
    vi.spyOn(enrolApi, 'preview').mockResolvedValue({ ...base, hasAccount: true });
    const redeem = vi.spyOn(enrolApi, 'redeem').mockResolvedValue({ accessToken: 'a2', refreshToken: 'r2' });
    renderEnrol();
    await screen.findByRole('heading', { name: "Join Ana's Store" });
    expect(screen.queryByLabelText(/^Type it again/)).not.toBeInTheDocument();

    fill('Your existing password', 'what they already use');
    fill('Choose a PIN', '903412');
    fireEvent.click(screen.getByRole('button', { name: 'Join' }));
    await waitFor(() => expect(redeem).toHaveBeenCalledWith('tok', 'what they already use', '903412', null));
  });

  it('chooses a new password for a reset link, with the PIN optional', async () => {
    vi.spyOn(enrolApi, 'preview').mockResolvedValue({ ...base, purpose: 1, hasAccount: true });
    const redeem = vi.spyOn(enrolApi, 'redeem').mockResolvedValue({ accessToken: 'a3', refreshToken: 'r3' });
    renderEnrol();
    expect(await screen.findByRole('heading', { name: 'Choose a new password' })).toBeInTheDocument();

    fill('New password', 'a brand new passphrase');
    fill('Type it again', 'a brand new passphrase');
    fireEvent.click(screen.getByRole('button', { name: 'Save password' }));
    await waitFor(() => expect(redeem).toHaveBeenCalledWith('tok', 'a brand new passphrase', null, null));
  });

  it('says so when the link was used or expired while the form was open', async () => {
    vi.spyOn(enrolApi, 'preview').mockResolvedValue(base);
    vi.spyOn(enrolApi, 'redeem').mockRejectedValue(new ApiError('notFound', 'raw'));
    renderEnrol();
    await screen.findByRole('heading', { name: "Join Ana's Store" });
    fill('Choose a password', 'long enough pass');
    fill('Type it again', 'long enough pass');
    fill('Choose a PIN', '482112');
    fireEvent.click(screen.getByRole('button', { name: 'Join' }));
    expect(await screen.findByRole('alert')).toHaveTextContent(/just used or has expired/);
    expect(useAuthStore.getState().accessToken).toBeNull();
  });
});
