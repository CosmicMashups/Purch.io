import { useState, useRef, useEffect } from 'react';
import { BrandMark, Wordmark } from '../../components/brand/Brand';
import { Link, Navigate, useNavigate } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { gsap } from 'gsap';
import { EnvelopeSimple, LockKey, Eye, EyeSlash } from '@phosphor-icons/react';
import { authApi, type BusinessChoice } from './api';
import { useSession } from './useSession';
import { useAuthStore } from '../../lib/authStore';
import { ApiError, userMessage } from '../../lib/apiError';

const adminSchema = z.object({
  email: z.string().trim().email('Enter a valid email'),
  password: z.string().min(1, 'Enter your password'),
});

type AdminForm = z.infer<typeof adminSchema>;

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

interface FormFieldProps {
  id: string;
  label: string;
  icon: React.ReactNode;
  error?: string;
  children: React.ReactNode;
}

function FormFieldWrapper({
  id,
  label,
  icon,
  error,
  children,
}: FormFieldProps) {
  const containerRef = useRef<HTMLDivElement>(null);

  // Subtle shake when error occurs
  useEffect(() => {
    if (!error || !containerRef.current) return;
    gsap.fromTo(
      containerRef.current,
      { x: -6 },
      { x: 0, duration: 0.35, ease: 'elastic.out(1, 0.4)' }
    );
  }, [error]);

  return (
    <div ref={containerRef} className="gsap-field-item flex flex-col gap-1.5">
      <label
        htmlFor={id}
        className="flex items-center gap-2 text-sm font-semibold text-ink"
      >
        <span className="text-ink-soft">{icon}</span>
        <span>{label}</span>
      </label>

      <div
        className={`group relative flex items-center rounded-control border bg-surface transition-all duration-200 ${
          error
            ? 'border-danger ring-1 ring-danger'
            : 'border-line focus-within:border-brand focus-within:ring-2 focus-within:ring-brand/20 hover:border-ink-soft/40'
        }`}
      >
        {children}
      </div>

      <FieldError id={`${id}-error`} message={error} />
    </div>
  );
}

export function LoginPage() {
  const navigate = useNavigate();
  const { claims } = useSession();
  const setTokens = useAuthStore((s) => s.setTokens);
  const [formError, setFormError] = useState<string | null>(null);
  const [showAdminPassword, setShowAdminPassword] = useState(false);
  const [choices, setChoices] = useState<{ email: string; password: string; businesses: BusinessChoice[] } | null>(null);

  const formContainerRef = useRef<HTMLDivElement>(null);
  const errorRef = useRef<HTMLParagraphElement>(null);

  const admin = useForm<AdminForm>({ resolver: zodResolver(adminSchema), defaultValues: { email: '', password: '' } });

  // GSAP stagger entry animation on mount
  useEffect(() => {
    if (!formContainerRef.current) return;
    const ctx = gsap.context(() => {
      gsap.fromTo(
        '.gsap-field-item',
        { opacity: 0, y: 12 },
        {
          opacity: 1,
          y: 0,
          duration: 0.35,
          stagger: 0.06,
          ease: 'power2.out',
          clearProps: 'transform,opacity',
        }
      );
    }, formContainerRef);

    return () => ctx.revert();
  }, []);

  // Subtle alert bounce on error update
  useEffect(() => {
    if (!formError || !errorRef.current) return;
    const ctx = gsap.context(() => {
      gsap.fromTo(
        errorRef.current,
        { x: -6, opacity: 0.8 },
        { x: 0, opacity: 1, duration: 0.35, ease: 'elastic.out(1, 0.4)' }
      );
    });
    return () => ctx.revert();
  }, [formError]);

  if (claims) return <Navigate to="/" replace />;

  /** Email and password. A person who belongs to several businesses is asked which one, then signs in again with it. */
  async function emailSignIn(email: string, password: string, tenantId?: string) {
    setFormError(null);
    try {
      const result = await authApi.signIn(email, password, tenantId);
      if ('chooseBusiness' in result && result.chooseBusiness) {
        setChoices({ email, password, businesses: result.businesses });
        return;
      }
      const { accessToken, refreshToken } = result as { accessToken: string; refreshToken: string };
      setTokens(accessToken, refreshToken);
      navigate('/', { replace: true });
    } catch (error) {
      setChoices(null);
      setFormError(loginMessage(error));
    }
  }

  const submitAdmin = admin.handleSubmit((v) => emailSignIn(v.email, v.password));

  return (
    <div className="grid min-h-dvh lg:grid-cols-[minmax(0,5.5fr)_minmax(0,4.5fr)]">
      <aside className="relative hidden flex-col justify-between overflow-hidden p-12 text-on-brand lg:flex">
        {/* Hero Background Image */}
        <img
          src="/login_hero.jpg"
          alt="Retail and cafe counter cashier smiling"
          className="absolute inset-0 h-full w-full object-cover object-[center_100%]"
        />
        {/* Multi-stage rich viridian / pine teal vignette overlay */}
        <div className="absolute inset-0 bg-gradient-to-b from-[#0A2E2B]/85 via-[#0F766E]/60 to-[#071E1C]/92" />

        {/* Content over image */}
        <div className="relative z-10 flex items-center gap-4">
          <BrandMark size={56} className="border-white/40 shadow-sm" />
          <Wordmark height={34} onBrand />
        </div>

        <div className="relative z-10 max-w-lg">
          <p className="text-4xl font-bold leading-tight tracking-tight text-white drop-shadow-sm">
            One till for everything you sell.
          </p>
          <p className="mt-3 text-base leading-relaxed text-white/85">
            Designed for high-volume retail, dining, and multi-location counters. Fast, offline-resilient checkout with unified inventory.
          </p>
        </div>
      </aside>

      <main className="flex flex-col items-center justify-center px-4 py-8">
        <div className="w-full max-w-md">
          {/* Mobile Header Banner with subtle hero image backdrop */}
          <div className="relative mb-6 overflow-hidden rounded-panel bg-brand p-5 text-on-brand shadow-sm lg:hidden">
            <img
              src="/login_hero.jpg"
              alt=""
              aria-hidden="true"
              className="absolute inset-0 h-full w-full object-cover object-right opacity-25"
            />
            <div className="relative z-10 flex items-center gap-3">
              <BrandMark size={40} className="border-white/40 shadow-sm" />
              <div>
                <Wordmark height={24} onBrand />
                <p className="mt-0.5 text-xs text-white/80">One till for everything you sell</p>
              </div>
            </div>
          </div>

          <h1 className="text-3xl font-bold tracking-tight">Sign in</h1>

          {formError && (
            <p
              ref={errorRef}
              role="alert"
              className="mt-4 rounded-control border border-danger bg-surface p-4 text-base font-medium text-danger"
            >
              {formError}
            </p>
          )}

          <div ref={formContainerRef} className="mt-6">
            {choices ? (
                <div className="flex flex-col gap-3" role="group" aria-label="Choose a business">
                  <p className="text-base font-semibold">Which business?</p>
                  {choices.businesses.map((b) => (
                    <button
                      key={b.tenantId}
                      type="button"
                      onClick={() => void emailSignIn(choices.email, choices.password, b.tenantId)}
                      className="h-14 rounded-control border border-line bg-surface px-4 text-left text-lg font-semibold hover:border-brand"
                    >
                      {b.name}
                    </button>
                  ))}
                </div>
              ) : (
              <form onSubmit={submitAdmin} noValidate className="flex flex-col gap-5">
                <FormFieldWrapper
                  id="email"
                  label="Email"
                  icon={<EnvelopeSimple size={18} weight="bold" />}
                  error={admin.formState.errors.email?.message}
                >
                  <input
                    id="email"
                    type="email"
                    autoComplete="username"
                    placeholder="owner@yourstore.com"
                    aria-invalid={!!admin.formState.errors.email}
                    aria-describedby="email-error"
                    className="h-13 w-full rounded-control bg-transparent px-4 text-base font-medium text-ink placeholder:text-ink-soft/45 focus:outline-none"
                    {...admin.register('email')}
                  />
                </FormFieldWrapper>

                <FormFieldWrapper
                  id="password"
                  label="Password"
                  icon={<LockKey size={18} weight="bold" />}
                  error={admin.formState.errors.password?.message}
                >
                  <div className="relative flex w-full items-center">
                    <input
                      id="password"
                      type={showAdminPassword ? 'text' : 'password'}
                      autoComplete="current-password"
                      placeholder="Enter your password"
                      aria-invalid={!!admin.formState.errors.password}
                      aria-describedby="password-error"
                      className="h-13 w-full rounded-control bg-transparent pl-4 pr-11 text-base font-medium text-ink placeholder:text-ink-soft/45 focus:outline-none"
                      {...admin.register('password')}
                    />
                    <button
                      type="button"
                      aria-label={showAdminPassword ? 'Hide password' : 'Show password'}
                      onClick={() => setShowAdminPassword(!showAdminPassword)}
                      className="absolute right-3 p-1.5 text-ink-soft transition-colors hover:text-ink focus:outline-none cursor-pointer"
                    >
                      {showAdminPassword ? <EyeSlash size={20} /> : <Eye size={20} />}
                    </button>
                  </div>
                </FormFieldWrapper>

                <div className="gsap-field-item pt-2">
                  <SubmitButton busy={admin.formState.isSubmitting} />
                </div>
              </form>
              )}
          </div>

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
      className="h-14 w-full rounded-control bg-brand text-lg font-semibold text-on-brand transition-all hover:bg-brand-strong active:scale-[0.99] disabled:opacity-60 cursor-pointer shadow-sm hover:shadow"
    >
      {busy ? 'Signing in...' : 'Sign in'}
    </button>
  );
}
