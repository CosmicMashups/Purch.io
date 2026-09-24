import { useState } from 'react';
import { Link, Navigate } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation } from '@tanstack/react-query';
import { FormField, PrimaryButton, SecondaryButton, controlClass } from '../../components/forms/FormField';
import { useSession } from '../auth/useSession';
import { BusinessType, businessTypeLabels } from '../business/types';
import { onboardingApi, type BootstrapResult } from './api';
import { STEP_FIELDS, bootstrapSchema, toBootstrapBody, type BootstrapForm } from './rules';

const STEP_TITLES = ['Your business', 'Your first branch', 'Your admin account'] as const;

export function OnboardingPage() {
  const { claims } = useSession();
  const [step, setStep] = useState(0);
  const [result, setResult] = useState<BootstrapResult | null>(null);
  const create = useMutation({ mutationFn: (body: ReturnType<typeof toBootstrapBody>) => onboardingApi.bootstrap(body) });
  const {
    register,
    handleSubmit,
    trigger,
    formState: { errors },
  } = useForm<BootstrapForm>({
    resolver: zodResolver(bootstrapSchema),
    defaultValues: { tenantName: '', businessType: BusinessType.ConvenienceStore as number, branchName: '', adminName: '', adminPin: '', adminEmail: '', adminPassword: '', agreed: false },
  });

  if (claims && !result) return <Navigate to="/" replace />;

  async function next() {
    if (await trigger(STEP_FIELDS[step] as (keyof BootstrapForm)[])) setStep((s) => s + 1);
  }

  const submit = handleSubmit((v) => create.mutate(toBootstrapBody(v), { onSuccess: setResult }));

  if (result) {
    return (
      <Shell title="You are all set">
        <p className="text-base text-ink-soft">Your business is ready. This is the code for your first register. Keep it safe: you enter it on the device together with your PIN.</p>
        <p className="mt-6 rounded-panel border border-brand bg-brand-tint p-6 text-center font-mono text-5xl font-bold tracking-widest" aria-label={`Device code ${result.devicePairingCode}`}>
          {result.devicePairingCode}
        </p>
        <p className="mt-4 text-base">On the sign-in screen choose Staff PIN, then enter this code and the PIN you just chose.</p>
        <Link to="/login" className="mt-6 inline-grid h-14 place-items-center rounded-control bg-brand px-8 text-lg font-semibold text-on-brand hover:bg-brand-strong">
          Go to sign in
        </Link>
      </Shell>
    );
  }

  return (
    <Shell title="Set up your business">
      <p aria-live="polite" className="text-base font-semibold text-ink-soft">
        Step {step + 1} of {STEP_TITLES.length}: {STEP_TITLES[step]}
      </p>
      <div role="progressbar" aria-valuemin={1} aria-valuemax={STEP_TITLES.length} aria-valuenow={step + 1} aria-label="Setup progress" className="mt-2 flex gap-2">
        {STEP_TITLES.map((title, i) => (
          <span key={title} className={`h-2 flex-1 rounded-full ${i <= step ? 'bg-brand' : 'bg-line'}`} />
        ))}
      </div>

      <form onSubmit={submit} noValidate className="mt-6 flex flex-col gap-5">
        {step === 0 && (
          <>
            <FormField label="Business name" error={errors.tenantName?.message}>
              <input {...register('tenantName')} autoComplete="organization" className={controlClass} />
            </FormField>
            <FormField label="What kind of business is it?">
              <select {...register('businessType', { valueAsNumber: true })} className={controlClass}>
                {Object.values(BusinessType).map((t) => (
                  <option key={t} value={t}>
                    {businessTypeLabels[t]}
                  </option>
                ))}
              </select>
            </FormField>
          </>
        )}

        {step === 1 && (
          <FormField label="Branch name" hint="Where you sell. You can add more branches later." error={errors.branchName?.message}>
            <input {...register('branchName')} className={controlClass} />
          </FormField>
        )}

        {step === 2 && (
          <>
            <FormField label="Your name" error={errors.adminName?.message}>
              <input {...register('adminName')} autoComplete="name" className={controlClass} />
            </FormField>
            <FormField label="Choose a PIN" hint="4 to 8 digits. You sign in on registers with it." error={errors.adminPin?.message}>
              <input type="password" inputMode="numeric" autoComplete="new-password" {...register('adminPin')} className={controlClass} />
            </FormField>
            <fieldset className="flex flex-col gap-4 rounded-panel border border-line p-4">
              <legend className="px-2 text-base font-semibold">Sign in from a computer (optional)</legend>
              <p className="text-sm text-ink-soft">Add an email and password to manage your business from this website. Leave both blank to use your PIN only.</p>
              <FormField label="Email" error={errors.adminEmail?.message}>
                <input type="email" autoComplete="email" {...register('adminEmail')} className={controlClass} />
              </FormField>
              <FormField label="Password" hint="8 characters or more" error={errors.adminPassword?.message}>
                <input type="password" autoComplete="new-password" {...register('adminPassword')} className={controlClass} />
              </FormField>
            </fieldset>
            <div>
              <label className="flex min-h-12 items-start gap-3 text-base">
                <input type="checkbox" {...register('agreed')} className="mt-1 size-6 shrink-0 accent-brand" />
                <span>
                  I have read and accept the{' '}
                  <Link to="/legal/terms" target="_blank" className="font-semibold text-brand-strong underline">
                    Terms of Service
                  </Link>{' '}
                  and the{' '}
                  <Link to="/legal/privacy" target="_blank" className="font-semibold text-brand-strong underline">
                    Privacy Policy
                  </Link>
                  .
                </span>
              </label>
              {errors.agreed && (
                <p role="alert" className="mt-1 text-sm font-medium text-danger">
                  {errors.agreed.message}
                </p>
              )}
            </div>
          </>
        )}

        <div className="flex flex-wrap gap-3">
          {step > 0 && (
            <SecondaryButton type="button" onClick={() => setStep((s) => s - 1)}>
              Back
            </SecondaryButton>
          )}
          {step < STEP_TITLES.length - 1 ? (
            <PrimaryButton type="button" onClick={() => void next()}>
              Next
            </PrimaryButton>
          ) : (
            <PrimaryButton type="submit" busy={create.isPending}>
              {create.isPending ? 'Creating...' : 'Create my business'}
            </PrimaryButton>
          )}
        </div>
      </form>

      <p className="mt-8 text-base text-ink-soft">
        Already set up?{' '}
        <Link to="/login" className="font-semibold text-brand-strong underline">
          Sign in
        </Link>
      </p>
    </Shell>
  );
}

function Shell({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <main className="mx-auto flex min-h-dvh max-w-xl flex-col justify-center px-4 py-10">
      <h1 className="mb-4 text-3xl font-bold tracking-tight">{title}</h1>
      {children}
    </main>
  );
}
