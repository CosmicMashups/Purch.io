import { Navigate, Outlet } from 'react-router-dom';
import { useAuthStore } from '../../lib/authStore';
import { readDeviceCredential } from '../kiosk/deviceCredential';
import { DEVICE_HOME, deviceRoleFromClaim } from '../kiosk/deviceRoles';
import { hasLeftTenant } from './signOut';
import { useSession } from './useSession';

export function RequireAuth() {
  const accessToken = useAuthStore((s) => s.accessToken);
  const { claims } = useSession();
  if (!accessToken) {
    // A paired till or warehouse device has its own lock screen; everyone else signs in with their email.
    // Signing out on purpose goes to the login page even when the device is paired.
    return <Navigate to={readDeviceCredential() && !hasLeftTenant() ? '/unlock' : '/login'} replace />;
  }
  // A kiosk or display token has no place in the staff shell; send it to its own screen.
  const device = deviceRoleFromClaim(claims?.role);
  if (device) return <Navigate to={DEVICE_HOME[device]} replace />;
  return <Outlet />;
}
