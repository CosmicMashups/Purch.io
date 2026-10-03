import { fireEvent, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../lib/apiError';
import { useAuthStore } from '../../lib/authStore';
import { renderPage } from '../../test/render';
import { authApi } from './api';
import { LoginPage } from './LoginPage';

beforeEach(() => {
  vi.restoreAllMocks();
  window.localStorage.clear();
  useAuthStore.setState({ accessToken: null, refreshToken: null });
});

function renderLogin() {
  return renderPage(<LoginPage />, { route: '/login', path: '/login', otherRoutes: [{ path: '/', element: <p>Home</p> }] });
}

function openEmail() {
  fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'ana@example.com' } });
  fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'correct horse battery' } });
}

describe('LoginPage email sign-in', () => {
  it('signs in with the account email and password', async () => {
    const signIn = vi.spyOn(authApi, 'signIn').mockResolvedValue({ accessToken: 'a1', refreshToken: 'r1' });
    renderLogin();
    openEmail();
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));
    expect(await screen.findByText('Home')).toBeInTheDocument();
    expect(signIn).toHaveBeenCalledWith('ana@example.com', 'correct horse battery', undefined);
    expect(useAuthStore.getState().accessToken).toBe('a1');
  });

  it('asks which business when the person belongs to several, then signs in to the one chosen', async () => {
    const signIn = vi
      .spyOn(authApi, 'signIn')
      .mockResolvedValueOnce({ chooseBusiness: true, businesses: [{ tenantId: 't1', name: 'First Store' }, { tenantId: 't2', name: 'Second Store' }] })
      .mockResolvedValueOnce({ accessToken: 'a2', refreshToken: 'r2' });
    renderLogin();
    openEmail();
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));
    fireEvent.click(await screen.findByRole('button', { name: 'Second Store' }));

    expect(await screen.findByText('Home')).toBeInTheDocument();
    expect(signIn).toHaveBeenLastCalledWith('ana@example.com', 'correct horse battery', 't2');
  });

  it('says the details were not recognised when neither login accepts them', async () => {
    vi.spyOn(authApi, 'signIn').mockRejectedValue(new ApiError('unauthorized', 'raw'));
    renderLogin();
    openEmail();
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Incorrect sign-in details');
    await waitFor(() => expect(useAuthStore.getState().accessToken).toBeNull());
  });
});
