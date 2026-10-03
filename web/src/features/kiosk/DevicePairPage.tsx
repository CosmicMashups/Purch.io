import { useEffect, useState } from 'react';
import { Navigate, useNavigate } from 'react-router-dom';
import { ApiError, userMessage } from '../../lib/apiError';
import { useAuthStore } from '../../lib/authStore';
import { DeviceType, deviceTypeLabels, labelOf } from '../business/types';
import { useSession } from '../auth/useSession';
import { deviceApi, type DeviceSession } from './api';
import { clearDeviceCredential, readDeviceCredential, saveDeviceCredential } from './deviceCredential';
import { DEVICE_HOME, deviceRoleFromClaim, type DeviceRole } from './deviceRoles';

const ROLE_OF_TYPE: Record<number, DeviceRole | undefined> = {
  [DeviceType.Kiosk]: 'Kiosk',
  [DeviceType.KitchenDisplay]: 'KitchenDisplay',
  [DeviceType.OrderBoard]: 'OrderBoard',
  [DeviceType.CustomerDisplay]: 'CustomerDisplay',
};

const inputClass = 'h-16 w-full rounded-control border border-ink-soft/40 bg-surface px-4 text-center font-mono text-3xl font-bold uppercase tracking-widest text-ink focus:border-brand';

/**
 * Pairs this browser as a device with the one-time code an Admin made on the Devices page. The code works once and expires,
 * and in exchange the device keeps its own credential, so nothing has to be typed again.
 */
export function DevicePairPage() {
  const navigate = useNavigate();
  const { claims } = useSession();
  const setTokens = useAuthStore((s) => s.setTokens);
  const accessToken = useAuthStore((s) => s.accessToken);
  const [code, setCode] = useState('');
  const [busy, setBusy] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const [paired, setPaired] = useState<DeviceSession | null>(null);

  function begin(session: DeviceSession) {
    const role = ROLE_OF_TYPE[session.deviceType];
    if (role && session.accessToken && session.refreshToken) {
      setTokens(session.accessToken, session.refreshToken);
      navigate(DEVICE_HOME[role], { replace: true });
      return;
    }
    // A till or warehouse device is operated by a person: its own lock screen takes it from here.
    if (session.requiresStaff) {
      navigate('/unlock', { replace: true });
      return;
    }
    setPaired(session);
  }

  // A device that was paired before and lost its session picks up where it left off, without a new code.
  useEffect(() => {
    const credential = readDeviceCredential();
    if (!credential || accessToken) return;
    deviceApi
      .startSession(credential)
      .then(begin)
      .catch((error: unknown) => {
        if (error instanceof ApiError && error.kind === 'unauthorized') clearDeviceCredential();
      });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  if (deviceRoleFromClaim(claims?.role)) return <Navigate to={DEVICE_HOME[deviceRoleFromClaim(claims?.role)!]} replace />;

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    if (!code.trim()) return setFormError('Enter the pairing code');
    setFormError(null);
    setBusy(true);
    try {
      const device = await deviceApi.pair(code.trim());
      saveDeviceCredential(device.deviceCredential);
      begin(await deviceApi.startSession(device.deviceCredential));
    } catch (error) {
      setFormError(error instanceof ApiError && error.kind === 'unauthorized' ? 'That code was not recognised, has already been used, or has expired. Ask an Admin for a new one.' : userMessage(error));
    } finally {
      setBusy(false);
    }
  }

  if (paired) {
    return (
      <main className="mx-auto flex min-h-dvh max-w-md flex-col justify-center gap-3 p-6">
        <h1 className="text-3xl font-bold tracking-tight">This device is paired</h1>
        <p className="text-base">
          {paired.name ?? labelOf(deviceTypeLabels, paired.deviceType)} is set up as a {labelOf(deviceTypeLabels, paired.deviceType).toLowerCase()}.
        </p>
        <p className="text-base text-ink-soft">Staff sign-in on paired devices, and the customer display feed, are not switched on yet.</p>
      </main>
    );
  }

  return (
    <main className="mx-auto flex min-h-dvh max-w-md flex-col justify-center gap-6 p-6">
      <div>
        <h1 className="text-3xl font-bold tracking-tight">Pair this device</h1>
        <p className="mt-2 text-base text-ink-soft">Ask an Admin to add this device under Business, Devices, then type the code they see.</p>
      </div>
      <form onSubmit={(event) => void submit(event)} noValidate className="flex flex-col gap-4">
        <label className="flex flex-col gap-1 text-base font-semibold">
          Pairing code
          <input value={code} onChange={(e) => setCode(e.target.value)} autoComplete="off" autoCapitalize="characters" spellCheck={false} maxLength={12} className={inputClass} />
        </label>
        {formError && (
          <p role="alert" className="rounded-control border border-danger/40 bg-danger/10 px-3 py-2 text-base font-medium text-danger">
            {formError}
          </p>
        )}
        <button type="submit" disabled={busy} className="h-14 rounded-control bg-brand text-lg font-bold text-on-brand disabled:opacity-60">
          {busy ? 'Pairing…' : 'Pair this device'}
        </button>
      </form>
    </main>
  );
}
