import { render } from '@testing-library/react';
import { MutationCache, QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactElement } from 'react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { useAuthStore } from '../lib/authStore';
import { notifyMutationError } from '../lib/queryClient';

function b64(o: object): string {
  return btoa(JSON.stringify(o)).replace(/=+$/, '').replace(/\+/g, '-').replace(/\//g, '_');
}

/** Puts an unsigned token for the given claims in the auth store. Tests only; the API is always mocked. */
export function signInAs(role: string, extra: Record<string, unknown> = {}): void {
  useAuthStore.setState({ accessToken: `${b64({ alg: 'none' })}.${b64({ role, sub: 's1', tenant_id: 't1', ...extra })}.x`, refreshToken: 'r' });
}

interface Options {
  route?: string;
  path?: string;
  /** Other routes to render, for pages that navigate. */
  otherRoutes?: { path: string; element: ReactElement }[];
}

/** Renders a page inside a fresh query cache and router. `path` is the route pattern, for pages that read params. */
export function renderPage(ui: ReactElement, { route = '/', path = '*', otherRoutes = [] }: Options = {}) {
  // Same failure reporting as the app, so a refused save toasts here exactly as it does for staff.
  const client = new QueryClient({
    mutationCache: new MutationCache({ onError: (error, _variables, _context, mutation) => notifyMutationError(error, mutation.meta?.silent) }),
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={[route]}>
        <Routes>
          <Route path={path} element={ui} />
          {otherRoutes.map((r) => (
            <Route key={r.path} path={r.path} element={r.element} />
          ))}
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}
