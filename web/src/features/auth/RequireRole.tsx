import { Navigate, Outlet } from 'react-router-dom';
import type { StaffRole } from '../../permissions/navPolicy';
import { useSession } from './useSession';

/** Keeps other roles off a page the API would refuse them anyway. Presentation only. */
export function RequireRole({ allow, redirectTo = '/business' }: { allow: readonly StaffRole[]; redirectTo?: string }) {
  const { role } = useSession();
  if (!role || !allow.includes(role)) return <Navigate to={redirectTo} replace />;
  return <Outlet />;
}
