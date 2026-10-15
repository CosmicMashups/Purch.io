import { useParams } from 'react-router-dom';
import { Pill } from '../../../components/lists/QueryList';
import { purchaseRequestStatusLabel } from '../workflow';
import type { PurchaseRequestLine } from '../types';
import { usePurchaseRequests } from '../queries';
import { LinesTable, RecordDetail } from './RecordDetail';

export function PurchaseRequestDetailPage() {
  const { id } = useParams();
  const requests = usePurchaseRequests();

  return (
    <RecordDetail
      query={requests}
      id={id}
      notFound="This purchase request no longer exists"
      backTo={{ to: '/inventory/purchase-requests', label: 'Purchase requests' }}
      title={(r) => r.controlNumber}
      subtitle={(r) => `Requested ${r.requestDate}`}
      facts={(r) => [
        { label: 'Status', value: <Pill tone={purchaseRequestStatusLabel[r.status].tone}>{purchaseRequestStatusLabel[r.status].label}</Pill> },
        { label: 'Branch', value: r.branchName },
        { label: 'Requested by', value: r.requestedByName || 'unknown' },
        { label: 'Date requested', value: r.requestDate },
        { label: 'Needed by', value: r.dateNeeded },
        { label: 'Delivery address', value: r.deliveryAddress },
        { label: 'Remarks', value: r.remarks },
      ]}
    >
      {(r) => (
        <LinesTable<PurchaseRequestLine>
          label="Items requested"
          lines={r.lines}
          getId={(l) => l.id}
          nameOf={(l) => l.name}
          columns={[
            { header: 'Item', sortValue: (l) => l.name, cell: (l) => <span className="font-medium text-gray-900">{l.name}</span> },
            { header: 'Quantity', className: 'text-right tabular-nums', sortValue: (l) => l.quantity, cell: (l) => l.quantity },
            { header: 'Unit', className: 'text-gray-600', sortValue: (l) => l.unit, cell: (l) => l.unit },
          ]}
        />
      )}
    </RecordDetail>
  );
}
