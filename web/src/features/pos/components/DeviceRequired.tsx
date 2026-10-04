import { useState } from 'react';
import { userMessage } from '../../../lib/apiError';
import { signOut } from '../../auth/signOut';
import { startRegisterSession, type RegisterChoice } from '../../auth/registerSessionApi';
import { useSession } from '../../auth/useSession';

/**
 * The POS ties every sale to a paired device and its branch. A staff PIN sign-in carries that already; an Admin or
 * Manager who signed in by email picks a Register here instead of having to sign out.
 */
export function DeviceRequired() {
  const { role } = useSession();
  const canPick = role === 'Admin' || role === 'Manager';
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [registers, setRegisters] = useState<RegisterChoice[] | null>(null);

  async function start(deviceId?: string) {
    setBusy(true);
    setError(null);
    try {
      const result = await startRegisterSession(deviceId);
      if (!result.done) setRegisters(result.registers);
    } catch (e) {
      setError(userMessage(e));
    } finally {
      setBusy(false);
    }
  }

  const buttonClass = 'h-12 rounded-control bg-brand px-6 text-base font-semibold text-on-brand hover:bg-brand-strong disabled:opacity-50';

  if (canPick) {
    return (
      <section className="max-w-xl rounded-panel border border-line bg-surface p-8">
        <h1 className="text-2xl font-bold tracking-tight">Choose a Register to sell on</h1>
        <p className="mt-2 text-base text-ink-soft">Sales are recorded against a Register and its branch. You can use one without signing out.</p>
        {registers ? (
          <ul className="mt-6 flex flex-col gap-2">
            {registers.map((r) => (
              <li key={r.deviceId}>
                <button type="button" disabled={busy} onClick={() => void start(r.deviceId)} className={`${buttonClass} w-full text-left`}>
                  {r.name}
                </button>
              </li>
            ))}
          </ul>
        ) : (
          <button type="button" disabled={busy} onClick={() => void start()} className={`mt-6 ${buttonClass}`}>
            {busy ? 'Opening…' : 'Open the Cashier'}
          </button>
        )}
        {error && (
          <p role="alert" className="mt-3 text-sm font-medium text-danger">
            {error}
          </p>
        )}
      </section>
    );
  }

  return (
    <section className="max-w-xl rounded-panel border border-line bg-surface p-8">
      <h1 className="text-2xl font-bold tracking-tight">Sign in with a device to sell</h1>
      <p className="mt-2 text-base text-ink-soft">
        Sales are recorded against a paired device and its branch. Sign out, then choose Staff PIN and enter this device's code and your PIN.
      </p>
      <button type="button" onClick={() => void signOut()} className={`mt-6 ${buttonClass}`}>
        Sign out
      </button>
    </section>
  );
}
