import { Navigate, Outlet } from 'react-router-dom';
import { useAuthStore } from '../../lib/authStore';
import { DEVICE_HOME, deviceRoleFromClaim } from '../kiosk/deviceRoles';
import { useSession } from './useSession';

export function RequireAuth() {
  const accessToken = useAuthStore((s) => s.accessToken);
  const { claims } = useSession();
  if (!accessToken) {
    return <Navigate to="/login" replace />;
  }
  // A kiosk or display token has no place in the staff shell; send it to its own screen.
  const device = deviceRoleFromClaim(claims?.role);
  if (device) return <Navigate to={DEVICE_HOME[device]} replace />;
  return <Outlet />;
}
