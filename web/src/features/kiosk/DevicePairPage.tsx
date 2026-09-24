import { useState } from 'react';
import { Navigate, useNavigate } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { ApiError, userMessage } from '../../lib/apiError';
import { useAuthStore } from '../../lib/authStore';
import { useSession } from '../auth/useSession';
import { deviceApi } from './api';
import { DEVICE_HOME, deviceRoleFromClaim, type DeviceRole } from './deviceRoles';

const schema = z.object({
  devicePairingCode: z.string().trim().min(1, 'Enter the device code'),
  pairingPin: z.string().min(1, 'Enter the PIN'),
});
type Form = z.infer<typeof schema>;

const COPY: Record<DeviceRole, { title: string; hint: string }> = {
  Kiosk: { title: 'Set up this kiosk', hint: 'Use the code and PIN of a kiosk device from Business, Devices.' },
  KitchenDisplay: { title: 'Set up this kitchen display', hint: 'Use the code and PIN of a kitchen display device from Business, Devices.' },
  OrderBoard: { title: 'Set up this order board', hint: 'Use the code and PIN of an order board device from Business, Devices.' },
};

const inputClass = 'h-14 w-full rounded-control border border-ink-soft/40 bg-surface px-4 text-lg text-ink focus:border-brand';

export function DevicePairPage({ role }: { role: DeviceRole }) {
  const navigate = useNavigate();
  const { claims } = useSession();
  const setTokens = useAuthStore((s) => s.setTokens);
  const [formError, setFormError] = useState<string | null>(null);
  const { register, handleSubmit, formState } = useForm<Form>({ resolver: zodResolver(schema), defaultValues: { devicePairingCode: '', pairingPin: '' } });

  if (deviceRoleFromClaim(claims?.role) === role) return <Navigate to={DEVICE_HOME[role]} replace />;

  function onSubmit(values: Form) {
    setFormError(null);
    deviceApi
      .pair(role, values.devicePairingCode.trim(), values.pairingPin)
      .then(({ accessToken, refreshToken }) => {
        setTokens(accessToken, refreshToken);
        navigate(DEVICE_HOME[role], { replace: true });
      })
      .catch((error: unknown) =>
        setFormError(error instanceof ApiError && error.kind === 'unauthorized' ? 'That code and PIN were not recognised, or belong to a different kind of device.' : userMessage(error)),
      );
  }

  const errors = formState.errors;
  return (
    <main className="mx-auto flex min-h-dvh max-w-md flex-col justify-center gap-6 p-6">
      <div>
        <h1 className="text-3xl font-bold tracking-tight">{COPY[role].title}</h1>
        <p className="mt-2 text-base text-ink-soft">{COPY[role].hint}</p>
      </div>
      <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex flex-col gap-4">
        <label className="flex flex-col gap-1 text-base font-semibold">
          Device code
          <input {...register('devicePairingCode')} autoComplete="off" autoCapitalize="characters" className={inputClass} aria-invalid={errors.devicePairingCode ? true : undefined} />
          {errors.devicePairingCode && <span role="alert" className="text-sm font-medium text-danger">{errors.devicePairingCode.message}</span>}
        </label>
        <label className="flex flex-col gap-1 text-base font-semibold">
          PIN
          <input {...register('pairingPin')} type="password" inputMode="numeric" autoComplete="off" className={inputClass} aria-invalid={errors.pairingPin ? true : undefined} />
          {errors.pairingPin && <span role="alert" className="text-sm font-medium text-danger">{errors.pairingPin.message}</span>}
        </label>
        {formError && <p role="alert" className="rounded-control border border-danger/40 bg-danger/10 px-3 py-2 text-base font-medium text-danger">{formError}</p>}
        <button type="submit" disabled={formState.isSubmitting} className="h-14 rounded-control bg-brand text-lg font-bold text-on-brand disabled:opacity-60">
          {formState.isSubmitting ? 'Pairing…' : 'Pair this device'}
        </button>
      </form>
    </main>
  );
}
