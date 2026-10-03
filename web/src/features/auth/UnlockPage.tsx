import { useEffect, useState } from 'react';
import { Navigate, useNavigate } from 'react-router-dom';
import { BackspaceIcon } from './BackspaceIcon';
import { ApiError, userMessage } from '../../lib/apiError';
import { useAuthStore } from '../../lib/authStore';
import { BrandMark } from '../../components/brand/Brand';
import { DeviceType } from '../business/types';
import { clearDeviceCredential, readDeviceCredential } from '../kiosk/deviceCredential';
import { PAIR_PATH } from '../kiosk/deviceRoles';
import { unlockApi, type DeviceRoster, type RosterPerson } from './unlockApi';
import { useSession } from './useSession';

const KEYS = ['1', '2', '3', '4', '5', '6', '7', '8', '9', '', '0', 'back'] as const;

/** Where a person lands: a Warehouse device opens Inventory, a Register opens the till. */
export function landingFor(deviceType: number): string {
  return deviceType === DeviceType.WarehouseOfficer ? '/inventory' : '/sell';
}

/**
 * The lock screen of a paired Register or Warehouse device: pick your name, type your own PIN. The device was paired once
 * with a one-time code and keeps its own credential, so nobody types a password here. A few wrong PINs lock only that person.
 */
export function UnlockPage() {
  const navigate = useNavigate();
  const { claims } = useSession();
  const setTokens = useAuthStore((s) => s.setTokens);
  const credential = readDeviceCredential();
  const [roster, setRoster] = useState<DeviceRoster | null>(null);
  const [loadFailed, setLoadFailed] = useState<'gone' | 'other' | null>(null);
  const [person, setPerson] = useState<RosterPerson | null>(null);
  const [pin, setPin] = useState('');
  const [message, setMessage] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (!credential) return;
    unlockApi
      .roster(credential)
      .then(setRoster)
      .catch((error: unknown) => {
        if (error instanceof ApiError && error.kind === 'unauthorized') {
          clearDeviceCredential();
          setLoadFailed('gone');
        } else setLoadFailed('other');
      });
  }, [credential]);

  if (claims) return <Navigate to="/" replace />;
  if (!credential || loadFailed === 'gone') {
    return (
      <main className="mx-auto flex min-h-dvh max-w-md flex-col justify-center gap-3 p-6">
        <h1 className="text-3xl font-bold tracking-tight">This device is not set up</h1>
        <p className="text-base text-ink-soft">It was never paired, or an Admin revoked it. Pair it with a new code from Business, Devices.</p>
        <a href={PAIR_PATH} className="grid h-14 place-items-center rounded-control bg-brand text-lg font-bold text-on-brand">
          Pair this device
        </a>
      </main>
    );
  }

  async function submit(chosen: RosterPerson, typed: string) {
    if (!credential || busy) return;
    setBusy(true);
    setMessage(null);
    try {
      const session = await unlockApi.unlock(credential, chosen.membershipId, typed);
      setTokens(session.accessToken, session.refreshToken);
      navigate(chosen.role === 2 ? landingFor(roster?.deviceType ?? DeviceType.Register) : '/', { replace: true });
    } catch (error) {
      setPin('');
      setMessage(error instanceof ApiError && (error.kind === 'unauthorized' || error.kind === 'rateLimited') ? error.message : userMessage(error));
    } finally {
      setBusy(false);
    }
  }

  function press(key: (typeof KEYS)[number]) {
    if (!person || busy) return;
    if (key === 'back') return setPin((p) => p.slice(0, -1));
    if (key === '') return;
    const next = (pin + key).slice(0, 8);
    setPin(next);
  }

  return (
    <main className="mx-auto flex min-h-dvh max-w-md flex-col justify-center gap-6 p-6">
      <div className="flex items-center gap-3">
        <BrandMark size={48} />
        <div>
          <h1 className="text-2xl font-bold tracking-tight">{roster?.deviceName ?? 'This device'} is locked</h1>
          <p className="text-base text-ink-soft">{person ? `Hello, ${person.name}. Type your PIN.` : 'Choose your name.'}</p>
        </div>
      </div>

      {loadFailed === 'other' && <p role="alert" className="rounded-control border border-danger/40 bg-danger/10 px-3 py-2 text-base font-medium text-danger">The list of people could not be loaded. Check the connection and reload.</p>}
      {!roster && !loadFailed && <p className="text-base text-ink-soft">Loading…</p>}

      {roster && !person && (
        <ul className="grid gap-2" aria-label="People">
          {roster.people.map((p) => (
            <li key={p.membershipId}>
              <button
                type="button"
                onClick={() => {
                  setPerson(p);
                  setMessage(null);
                }}
                className="h-16 w-full rounded-control border border-line bg-surface px-4 text-left text-lg font-semibold hover:border-brand"
              >
                {p.name}
              </button>
            </li>
          ))}
          {roster.people.length === 0 && <li className="text-base text-ink-soft">No one is set up to work on this device yet. Ask an Admin to add you under Business, Staff.</li>}
        </ul>
      )}

      {person && (
        <form
          onSubmit={(event) => {
            event.preventDefault();
            void submit(person, pin);
          }}
          className="flex flex-col gap-4"
        >
          <p aria-label="PIN entered" className="h-12 text-center font-mono text-3xl tracking-[0.5em]">
            {'•'.repeat(pin.length)}
          </p>
          <div className="grid grid-cols-3 gap-2">
            {KEYS.map((key, i) =>
              key === '' ? (
                <span key={i} />
              ) : (
                <button key={key} type="button" onClick={() => press(key)} aria-label={key === 'back' ? 'Delete' : key} className="grid h-16 place-items-center rounded-control border border-line bg-surface text-2xl font-semibold hover:border-brand active:translate-y-px">
                  {key === 'back' ? <BackspaceIcon /> : key}
                </button>
              ),
            )}
          </div>
          {message && (
            <p role="alert" className="rounded-control border border-danger/40 bg-danger/10 px-3 py-2 text-base font-medium text-danger">
              {message}
            </p>
          )}
          <button type="submit" disabled={busy || pin.length < 4} className="h-14 rounded-control bg-brand text-lg font-bold text-on-brand disabled:opacity-50">
            {busy ? 'Checking…' : 'Unlock'}
          </button>
          <button type="button" onClick={() => { setPerson(null); setPin(''); setMessage(null); }} className="h-12 text-base font-semibold text-brand-strong underline">
            Not you? Choose someone else
          </button>
        </form>
      )}
    </main>
  );
}
