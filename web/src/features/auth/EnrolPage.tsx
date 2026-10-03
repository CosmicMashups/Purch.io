import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { apiClient } from '../../lib/apiClient';
import { ApiError, userMessage } from '../../lib/apiError';
import { useAuthStore } from '../../lib/authStore';
import { MembershipRole, describeDuties, membershipRoleLabels, labelOf } from '../business/types';

interface Preview {
  businessName: string;
  name: string;
  email: string;
  purpose: number;
  role: number;
  duties: number;
  hasAccount: boolean;
}

export const enrolApi = {
  preview: (token: string) => apiClient.post<Preview>('/enrol/preview', { token }).then((r) => r.data),
  redeem: (token: string, password: string, pin: string | null, email: string | null = null) =>
    apiClient.post<{ accessToken: string; refreshToken: string }>('/enrol/redeem', { token, password, pin, email }).then((r) => r.data),
};

const inputClass = 'h-14 w-full rounded-control border border-ink-soft/40 bg-surface px-4 text-lg text-ink focus:border-brand';

/**
 * Where a single-use link from an Admin or Manager lands (on the person's own phone). It sets a password and a personal PIN
 * for a new person, or a new password for someone who forgot theirs, then signs them in. No email is involved.
 */
export function EnrolPage() {
  const { token = '' } = useParams<{ token: string }>();
  const navigate = useNavigate();
  const setTokens = useAuthStore((s) => s.setTokens);
  const [preview, setPreview] = useState<Preview | null>(null);
  const [unusable, setUnusable] = useState(false);
  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [pin, setPin] = useState('');
  const [email, setEmail] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    enrolApi
      .preview(token)
      .then(setPreview)
      .catch(() => setUnusable(true));
  }, [token]);

  if (unusable) {
    return (
      <main className="mx-auto flex min-h-dvh max-w-md flex-col justify-center gap-3 p-6">
        <h1 className="text-3xl font-bold tracking-tight">This link cannot be used</h1>
        <p className="text-base text-ink-soft">It was already used, was cancelled, or has expired. Ask your manager for a new one.</p>
      </main>
    );
  }
  if (!preview) return <main className="grid min-h-dvh place-items-center p-6 text-base text-ink-soft">Checking your link…</main>;

  const reset = preview.purpose === 1;
  const newAccount = !reset && !preview.hasAccount;
  // An owner from the old PIN-only sign-in has no email on file yet, so they give one here.
  const needsEmail = !reset && !preview.email;

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    if (needsEmail && !/^\S+@\S+\.\S+$/.test(email.trim())) return setError('Enter a valid email address.');
    if (newAccount || reset) {
      if (password.length < 8) return setError('Use at least 8 characters for the password.');
      if (password !== confirm) return setError('The two passwords do not match.');
    } else if (!password) {
      return setError('Enter your existing password.');
    }
    if (!reset && !/^\d{4,8}$/.test(pin)) return setError('Choose a PIN of 4 to 8 digits.');
    if (reset && pin && !/^\d{4,8}$/.test(pin)) return setError('A PIN is 4 to 8 digits.');

    setError(null);
    setBusy(true);
    try {
      const session = await enrolApi.redeem(token, password, pin || null, needsEmail ? email.trim() : null);
      setTokens(session.accessToken, session.refreshToken);
      navigate('/', { replace: true });
    } catch (failure) {
      setError(failure instanceof ApiError && failure.kind === 'notFound' ? 'This link was just used or has expired. Ask for a new one.' : userMessage(failure));
    } finally {
      setBusy(false);
    }
  }

  return (
    <main className="mx-auto flex min-h-dvh max-w-md flex-col justify-center gap-6 p-6">
      <div>
        <h1 className="text-3xl font-bold tracking-tight">{reset ? 'Choose a new password' : `Join ${preview.businessName}`}</h1>
        <p className="mt-2 text-base text-ink-soft">
          {preview.name}{preview.email ? ` · ${preview.email}` : ''}
          {!reset && (
            <>
              <br />
              {labelOf(membershipRoleLabels, preview.role)}
              {preview.role === MembershipRole.Staff ? `, ${describeDuties(preview.duties)}` : ''}
            </>
          )}
        </p>
      </div>
      <form onSubmit={(event) => void submit(event)} noValidate className="flex flex-col gap-4">
        {needsEmail && (
          <label className="flex flex-col gap-1 text-base font-semibold">
            Your email
            <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} autoComplete="email" className={inputClass} />
            <span className="text-sm font-normal text-ink-soft">You will sign in with this from now on.</span>
          </label>
        )}
        <label className="flex flex-col gap-1 text-base font-semibold">
          {newAccount || reset ? (reset ? 'New password' : 'Choose a password') : 'Your existing password'}
          <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} autoComplete={newAccount || reset ? 'new-password' : 'current-password'} className={inputClass} />
          {!newAccount && !reset && <span className="text-sm font-normal text-ink-soft">You already have an account, so this joins {preview.businessName} to it.</span>}
        </label>
        {(newAccount || reset) && (
          <label className="flex flex-col gap-1 text-base font-semibold">
            Type it again
            <input type="password" value={confirm} onChange={(e) => setConfirm(e.target.value)} autoComplete="new-password" className={inputClass} />
          </label>
        )}
        <label className="flex flex-col gap-1 text-base font-semibold">
          {reset ? 'New PIN (optional)' : 'Choose a PIN'}
          <input type="password" inputMode="numeric" value={pin} onChange={(e) => setPin(e.target.value)} autoComplete="off" className={inputClass} />
          <span className="text-sm font-normal text-ink-soft">4 to 8 digits. You type it at the till to unlock it.</span>
        </label>
        {error && (
          <p role="alert" className="rounded-control border border-danger/40 bg-danger/10 px-3 py-2 text-base font-medium text-danger">
            {error}
          </p>
        )}
        <button type="submit" disabled={busy} className="h-14 rounded-control bg-brand text-lg font-bold text-on-brand disabled:opacity-60">
          {busy ? 'Saving…' : reset ? 'Save password' : 'Join'}
        </button>
      </form>
    </main>
  );
}
