import { fireEvent, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { useToastStore } from '../../components/feedback/toastStore';
import { ApiError } from '../../lib/apiError';
import { useAuthStore } from '../../lib/authStore';
import { renderPage, signInAs } from '../../test/render';
import { onboardingApi } from './api';
import { LegalPage } from './LegalPage';
import { OnboardingPage } from './OnboardingPage';
import { PRIVACY_POLICY, TERMS_OF_SERVICE } from './legalContent';

vi.mock('./api', () => ({ onboardingApi: { bootstrap: vi.fn() } }));

beforeEach(() => {
  vi.clearAllMocks();
  useToastStore.setState({ toasts: [] });
  useAuthStore.setState({ accessToken: null, refreshToken: null });
  vi.mocked(onboardingApi.bootstrap).mockResolvedValue({ tenantId: 't', branchId: 'b', deviceId: 'd', devicePairingCode: 'KAP-2026', adminUserId: 'u' });
});

const type = (target: string | RegExp | HTMLElement, value: string) =>
  fireEvent.change(typeof target === 'string' || target instanceof RegExp ? screen.getByLabelText(target) : target, { target: { value } });
const next = () => fireEvent.click(screen.getByRole('button', { name: 'Next' }));

async function toStepThree() {
  renderPage(<OnboardingPage />, { route: '/onboarding', path: '/onboarding', otherRoutes: [{ path: '/', element: <p>Home</p> }, { path: '/login', element: <p>Sign in page</p> }] });
  type('Business name', 'Kape Katipunan');
  next();
  type(await screen.findByLabelText('Branch name'), 'Main');
  next();
  await screen.findByLabelText('Your name');
}

describe('OnboardingPage', () => {
  it('holds each step until it is valid', async () => {
    renderPage(<OnboardingPage />);
    next();
    expect(await screen.findByText('Enter your business name')).toBeInTheDocument();
    expect(screen.getByText('Step 1 of 3: Your business')).toBeInTheDocument();

    type('Business name', 'Kape');
    next();
    expect(await screen.findByText('Step 2 of 3: Your first branch')).toBeInTheDocument();
    next();
    expect(await screen.findByText('Enter your first branch name')).toBeInTheDocument();
  });

  it('keeps what was typed when going back', async () => {
    renderPage(<OnboardingPage />);
    type('Business name', 'Kape');
    next();
    fireEvent.click(await screen.findByRole('button', { name: 'Back' }));
    expect(await screen.findByLabelText('Business name')).toHaveValue('Kape');
  });

  it('creates a PIN-only business and shows the device code to enter on the register', async () => {
    await toStepThree();
    type('Your name', 'Mario Cruz');
    type('Choose a PIN', '4321');
    fireEvent.click(screen.getByRole('checkbox'));
    fireEvent.click(screen.getByRole('button', { name: 'Create my business' }));

    await waitFor(() => expect(onboardingApi.bootstrap).toHaveBeenCalledTimes(1));
    expect(onboardingApi.bootstrap).toHaveBeenCalledWith({
      tenantName: 'Kape Katipunan',
      businessType: 0,
      branchName: 'Main',
      adminName: 'Mario Cruz',
      adminPin: '4321',
      adminEmail: null,
      adminPassword: null,
    });
    expect(await screen.findByLabelText('Device code KAP-2026')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Go to sign in' })).toHaveAttribute('href', '/login');
  });

  it('sends an email and password together when both are given', async () => {
    await toStepThree();
    type('Your name', 'Mario Cruz');
    type('Choose a PIN', '4321');
    type('Email', 'mario@kape.ph');
    type('Password', 'longenough1');
    fireEvent.click(screen.getByRole('checkbox'));
    fireEvent.click(screen.getByRole('button', { name: 'Create my business' }));
    await waitFor(() => expect(onboardingApi.bootstrap).toHaveBeenCalledWith(expect.objectContaining({ adminEmail: 'mario@kape.ph', adminPassword: 'longenough1' })));
  });

  it('will not create anything without the agreement, a good PIN, or a matching email and password', async () => {
    await toStepThree();
    type('Your name', 'Mario Cruz');
    type('Choose a PIN', '12');
    type('Email', 'mario@kape.ph');
    fireEvent.click(screen.getByRole('button', { name: 'Create my business' }));
    expect(await screen.findByText('Use 4 to 8 digits')).toBeInTheDocument();
    expect(screen.getByText('Add a password too, or clear the email')).toBeInTheDocument();
    expect(screen.getByText('Please review and accept to continue')).toBeInTheDocument();
    expect(onboardingApi.bootstrap).not.toHaveBeenCalled();
  });

  it('links to the terms and privacy policy in a new tab', async () => {
    await toStepThree();
    expect(screen.getByRole('link', { name: 'Terms of Service' })).toHaveAttribute('href', '/legal/terms');
    expect(screen.getByRole('link', { name: 'Privacy Policy' })).toHaveAttribute('target', '_blank');
  });

  it('stays on the last step and shows the server message when it refuses', async () => {
    vi.mocked(onboardingApi.bootstrap).mockRejectedValue(new ApiError('validation', 'Admin PIN is already in use.'));
    await toStepThree();
    type('Your name', 'Mario Cruz');
    type('Choose a PIN', '4321');
    fireEvent.click(screen.getByRole('checkbox'));
    fireEvent.click(screen.getByRole('button', { name: 'Create my business' }));
    await waitFor(() => expect(useToastStore.getState().toasts[0]?.message).toBe('Admin PIN is already in use.'));
    expect(screen.queryByLabelText(/^Device code/)).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Create my business' })).toBeEnabled();
  });

  it('sends someone who is already signed in home instead of offering setup', () => {
    signInAs('Admin');
    renderPage(<OnboardingPage />, { route: '/onboarding', path: '/onboarding', otherRoutes: [{ path: '/', element: <p>Home</p> }] });
    expect(screen.getByText('Home')).toBeInTheDocument();
  });
});

describe('LegalPage', () => {
  const at = (document: string) => renderPage(<LegalPage />, { route: `/legal/${document}`, path: '/legal/:document', otherRoutes: [{ path: '/login', element: <p>Sign in page</p> }] });

  it('shows every section of the terms exactly as carried over', () => {
    at('terms');
    expect(screen.getByRole('heading', { level: 1, name: 'Terms of Service' })).toBeInTheDocument();
    expect(screen.getAllByRole('heading', { level: 2 })).toHaveLength(TERMS_OF_SERVICE.length);
    expect(screen.getByText(TERMS_OF_SERVICE[0].heading)).toBeInTheDocument();
  });

  it('shows the privacy policy', () => {
    at('privacy');
    expect(screen.getByRole('heading', { level: 1, name: 'Privacy Policy' })).toBeInTheDocument();
    expect(screen.getAllByRole('heading', { level: 2 })).toHaveLength(PRIVACY_POLICY.length);
  });

  it('sends an unknown document back to sign in', () => {
    at('cookies');
    expect(screen.getByText('Sign in page')).toBeInTheDocument();
  });

  it('carried over all 22 sections from the Flutter client', () => {
    expect(PRIVACY_POLICY.length + TERMS_OF_SERVICE.length).toBe(22);
    for (const s of [...PRIVACY_POLICY, ...TERMS_OF_SERVICE]) {
      expect(s.heading.length).toBeGreaterThan(0);
      expect(s.body.length).toBeGreaterThan(0);
    }
  });
});
