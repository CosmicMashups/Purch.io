import { Navigate, Outlet } from 'react-router-dom';
import { useAuthStore } from '../../lib/authStore';
import { signOut } from '../auth/signOut';
import { useSession } from '../auth/useSession';
import { DEVICE_LABEL, DEVICE_PAIR, deviceRoleFromClaim, type DeviceRole } from './deviceRoles';

/**
 * A device page needs a token for that device role. No token means it has not been paired (or its
 * session ended), so it goes to pairing. A token for anything else is never reused: this browser is
 * set up for a different purpose and says so, instead of showing a screen the API would refuse.
 */
export function RequireDevice({ role }: { role: DeviceRole }) {
  const accessToken = useAuthStore((s) => s.accessToken);
  const { claims } = useSession();
  if (!accessToken) return <Navigate to={DEVICE_PAIR[role]} replace />;

  const actual = deviceRoleFromClaim(claims?.role);
  if (actual !== role) {
    return (
      <main className="mx-auto flex min-h-dvh max-w-md flex-col justify-center gap-4 p-6">
        <h1 className="text-2xl font-bold">This browser is set up for something else</h1>
        <p className="text-base text-ink-soft">
          It is signed in as {actual ? DEVICE_LABEL[actual] : 'a staff member'}. To use it as {DEVICE_LABEL[role]} instead, sign out first, then pair it again.
        </p>
        <button type="button" onClick={() => void signOut()} className="h-14 rounded-control bg-brand px-6 text-lg font-bold text-on-brand">
          Sign out
        </button>
      </main>
    );
  }
  return <Outlet />;
}
