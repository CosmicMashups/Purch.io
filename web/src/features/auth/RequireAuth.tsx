import { Navigate, Outlet } from 'react-router-dom';
import { useAuthStore } from '../../lib/authStore';
import { readDeviceCredential } from '../kiosk/deviceCredential';
import { DEVICE_HOME, deviceRoleFromClaim } from '../kiosk/deviceRoles';
import { useSession } from './useSession';

export function RequireAuth() {
  const accessToken = useAuthStore((s) => s.accessToken);
  const { claims } = useSession();
  if (!accessToken) {
    // A paired till or warehouse device has its own lock screen; everyone else signs in with their email.
    return <Navigate to={readDeviceCredential() ? '/unlock' : '/login'} replace />;
  }
  // A kiosk or display token has no place in the staff shell; send it to its own screen.
  const device = deviceRoleFromClaim(claims?.role);
  if (device) return <Navigate to={DEVICE_HOME[device]} replace />;
  return <Outlet />;
}
