import { Navigate, useLocation } from 'react-router-dom';

/** Reports used to live under Business; old links and bookmarks land on the Reports tab with their query kept. */
export function ReportsRedirect() {
  const { search } = useLocation();
  return <Navigate to={`/reports${search}`} replace />;
}
