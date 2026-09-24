import { Navigate, Outlet } from 'react-router-dom';
import { tabsForRole, type AppTab } from '../../permissions/navPolicy';
import { useSession } from './useSession';

/** Keeps a role off a section it has no tab for. Presentation only: the API still rejects the calls. */
export function RequireTab({ tab }: { tab: AppTab }) {
  const { role } = useSession();
  if (!tabsForRole(role).includes(tab)) {
    return <Navigate to="/" replace />;
  }
  return <Outlet />;
}
