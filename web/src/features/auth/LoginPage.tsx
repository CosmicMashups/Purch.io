import { useState } from 'react';
import { BrandMark, Wordmark } from '../../components/brand/Brand';
import { Link, Navigate, useNavigate } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { authApi } from './api';
import { useSession } from './useSession';
import { useAuthStore } from '../../lib/authStore';
import { ApiError, userMessage } from '../../lib/apiError';

const PAIRING_CODE_KEY = 'purch.devicePairingCode';

function readPairingCode(): string {
  try {
    return window.localStorage.getItem(PAIRING_CODE_KEY) ?? '';
  } catch {
    return '';
  }
}

function rememberPairingCode(code: string): void {
  try {
    window.localStorage.setItem(PAIRING_CODE_KEY, code);
  } catch {
    // Not remembering the code only means it is typed again next time.
  }
}

const staffSchema = z.object({
  devicePairingCode: z.string().trim().min(1, 'Enter the device code'),
  pin: z.string().regex(/^\d{4,8}$/, 'PIN is 4 to 8 digits'),
});

const adminSchema = z.object({
  email: z.string().trim().email('Enter a valid email'),
  password: z.string().min(1, 'Enter your password'),
});

type StaffForm = z.infer<typeof staffSchema>;
type AdminForm = z.infer<typeof adminSchema>;
type Mode = 'staff' | 'admin';

const inputClass =
  'h-14 w-full rounded-control border border-ink-soft/40 bg-surface px-4 text-lg text-ink placeholder:text-ink-soft/70 focus:border-brand';

function FieldError({ id, message }: { id: string; message?: string }) {
  return message ? (
    <p id={id} role="alert" className="mt-1 text-sm font-medium text-danger">
      {message}
    </p>
  ) : null;
}

function loginMessage(error: unknown): string {
  if (error instanceof ApiError && error.kind === 'unauthorized') return 'Incorrect sign-in details. Check them and try again.';
  return userMessage(error);
}

export function LoginPage() {
  const navigate = useNavigate();
  const { claims } = useSession();
  const setTokens = useAuthStore((s) => s.setTokens);
  const [mode, setMode] = useState<Mode>('staff');
  const [formError, setFormError] = useState<string | null>(null);

  const staff = useForm<StaffForm>({
    resolver: zodResolver(staffSchema),
    defaultValues: { devicePairingCode: readPairingCode(), pin: '' },
  });
  const admin = useForm<AdminForm>({ resolver: zodResolver(adminSchema), defaultValues: { email: '', password: '' } });

  if (claims) return <Navigate to="/" replace />;

  async function finish(request: Promise<{ accessToken: string; refreshToken: string }>) {
    setFormError(null);
    try {
      const { accessToken, refreshToken } = await request;
      setTokens(accessToken, refreshToken);
      navigate('/', { replace: true });
    } catch (error) {
      staff.resetField('pin');
      setFormError(loginMessage(error));
    }
  }

  const submitStaff = staff.handleSubmit((v) => {
    rememberPairingCode(v.devicePairingCode);
    return finish(authApi.pinLogin(v.devicePairingCode, v.pin));
  });
  const submitAdmin = admin.handleSubmit((v) => finish(authApi.adminLogin(v.email, v.password)));

  const switchMode = (next: Mode) => {
    setMode(next);
    setFormError(null);
  };

  const tabClass = (active: boolean) =>
    `h-12 flex-1 rounded-control text-base font-semibold ${active ? 'bg-surface text-ink shadow-sm' : 'text-ink-soft'}`;

  return (
    <div className="grid min-h-dvh lg:grid-cols-[minmax(0,5fr)_minmax(0,4fr)]">
      <aside className="hidden flex-col justify-between bg-brand p-12 text-on-brand lg:flex">
        <div className="flex items-center gap-4">
          <BrandMark size={64} className="border-white/30" />
          <Wordmark height={36} onBrand />
        </div>
        <p className="max-w-md text-4xl font-bold leading-tight tracking-tight">One till for everything you sell.</p>
      </aside>

      <main className="flex items-center justify-center px-4 py-10">
        <div className="w-full max-w-md">
          <h1 className="text-3xl font-bold tracking-tight">Sign in</h1>

          <div role="group" aria-label="Sign-in type" className="mt-6 flex gap-1 rounded-panel bg-line/60 p-1">
            <button type="button" aria-pressed={mode === 'staff'} onClick={() => switchMode('staff')} className={tabClass(mode === 'staff')}>
              Staff PIN
            </button>
            <button type="button" aria-pressed={mode === 'admin'} onClick={() => switchMode('admin')} className={tabClass(mode === 'admin')}>
              Admin
            </button>
          </div>

          {formError && (
            <p role="alert" className="mt-4 rounded-control border border-danger bg-surface p-4 text-base font-medium text-danger">
              {formError}
            </p>
          )}

          {mode === 'staff' ? (
            <form onSubmit={submitStaff} noValidate className="mt-6 flex flex-col gap-5">
              <div>
                <label htmlFor="devicePairingCode" className="mb-2 block text-base font-semibold">
                  Device code
                </label>
                <input
                  id="devicePairingCode"
                  autoComplete="off"
                  autoCapitalize="characters"
                  aria-invalid={!!staff.formState.errors.devicePairingCode}
                  aria-describedby="devicePairingCode-error"
                  className={inputClass}
                  {...staff.register('devicePairingCode')}
                />
                <FieldError id="devicePairingCode-error" message={staff.formState.errors.devicePairingCode?.message} />
              </div>
              <div>
                <label htmlFor="pin" className="mb-2 block text-base font-semibold">
                  PIN
                </label>
                <input
                  id="pin"
                  type="password"
                  inputMode="numeric"
                  autoComplete="off"
                  aria-invalid={!!staff.formState.errors.pin}
                  aria-describedby="pin-error"
                  className={`${inputClass} tracking-[0.4em]`}
                  {...staff.register('pin')}
                />
                <FieldError id="pin-error" message={staff.formState.errors.pin?.message} />
              </div>
              <SubmitButton busy={staff.formState.isSubmitting} />
            </form>
          ) : (
            <form onSubmit={submitAdmin} noValidate className="mt-6 flex flex-col gap-5">
              <div>
                <label htmlFor="email" className="mb-2 block text-base font-semibold">
                  Email
                </label>
                <input
                  id="email"
                  type="email"
                  autoComplete="username"
                  aria-invalid={!!admin.formState.errors.email}
                  aria-describedby="email-error"
                  className={inputClass}
                  {...admin.register('email')}
                />
                <FieldError id="email-error" message={admin.formState.errors.email?.message} />
              </div>
              <div>
                <label htmlFor="password" className="mb-2 block text-base font-semibold">
                  Password
                </label>
                <input
                  id="password"
                  type="password"
                  autoComplete="current-password"
                  aria-invalid={!!admin.formState.errors.password}
                  aria-describedby="password-error"
                  className={inputClass}
                  {...admin.register('password')}
                />
                <FieldError id="password-error" message={admin.formState.errors.password?.message} />
              </div>
              <SubmitButton busy={admin.formState.isSubmitting} />
            </form>
          )}

          <p className="mt-8 text-base text-ink-soft">
            New business?{' '}
            <Link to="/onboarding" className="font-semibold text-brand-strong underline">
              Set up Purch.io
            </Link>
          </p>
          <p className="mt-2 text-sm text-ink-soft">
            <Link to="/legal/terms" className="underline">
              Terms of Service
            </Link>
            {' and '}
            <Link to="/legal/privacy" className="underline">
              Privacy Policy
            </Link>
          </p>
        </div>
      </main>
    </div>
  );
}

function SubmitButton({ busy }: { busy: boolean }) {
  return (
    <button
      type="submit"
      disabled={busy}
      className="h-14 rounded-control bg-brand text-lg font-semibold text-on-brand hover:bg-brand-strong active:translate-y-px disabled:opacity-60"
    >
      {busy ? 'Signing in...' : 'Sign in'}
    </button>
  );
}
