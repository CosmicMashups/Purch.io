import { Navigate, useSearchParams } from 'react-router-dom';

/** Recording a movement is a dialog now; an old `/inventory/movements/new?itemId=&type=` link opens it on the log. */
export function LegacyRecordMovementRedirect() {
  const [params] = useSearchParams();
  const next = new URLSearchParams({ record: '1' });
  const itemId = params.get('itemId');
  if (itemId) next.set('stockRef', `item:${itemId}`);
  const type = params.get('type');
  if (type) next.set('recordType', type);
  return <Navigate to={`/inventory/movements?${next.toString()}`} replace />;
}
