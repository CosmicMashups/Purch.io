import { useParams } from 'react-router-dom';
import { Link } from 'react-router-dom';
import { Pill } from '../../../components/lists/QueryList';
import { formatPeso } from '../../dashboard/format';
import { useIncomingReceiving, usePurchaseOrders } from '../queries';
import type { IncomingReceivingLine } from '../types';
import { conditionLabel, remarkLabel } from '../workflow';
import { LinesTable, RecordDetail } from './RecordDetail';

export function IncomingReceivingDetailPage() {
  const { id } = useParams();
  const reports = useIncomingReceiving();
  const orders = usePurchaseOrders();

  return (
    <RecordDetail
      query={reports}
      id={id}
      notFound="This delivery report no longer exists"
      backTo={{ to: '/inventory/incoming-receiving', label: 'Incoming receiving' }}
      title={(r) => r.controlNumber || `Delivery from ${r.supplierName}`}
      subtitle={(r) => `Delivery from ${r.supplierName}, ${r.deliveryDate}`}
      facts={(r) => {
        const linked = orders.data?.find((o) => o.id === r.purchaseOrderId);
        return [
          { label: 'Supplier', value: r.supplierName },
          { label: 'Delivered to', value: r.branchName },
          { label: 'Delivery date', value: r.deliveryDate },
          { label: 'Received by', value: r.receivedByName },
          {
            label: 'Purchase order',
            value:
              r.purchaseOrderId === null ? (
                <Pill tone="warn">Not linked</Pill>
              ) : (
                <Link to={`/inventory/purchase-orders/${r.purchaseOrderId}`} className="text-brand-strong underline">
                  {linked ? linked.controlNumber || `Order to ${linked.supplierName}` : 'Linked order'}
                </Link>
              ),
          },
          { label: 'Remarks', value: r.remarks },
        ];
      }}
    >
      {(r) => (
        <LinesTable<IncomingReceivingLine>
          label="Items received"
          lines={r.lines}
          getId={(l) => l.id}
          nameOf={(l) => l.itemName}
          columns={[
            { header: 'Item', sortValue: (l) => l.itemName, cell: (l) => <span className="font-medium text-gray-900">{l.itemName}</span> },
            { header: 'Received', className: 'text-right tabular-nums', sortValue: (l) => l.quantityReceived, cell: (l) => `${l.quantityReceived} ${l.uom}` },
            { header: 'Unit price', className: 'text-right tabular-nums', sortValue: (l) => l.unitPrice, cell: (l) => formatPeso(l.unitPrice) },
            { header: 'Condition', className: 'text-gray-600', sortValue: (l) => conditionLabel[l.condition], cell: (l) => conditionLabel[l.condition] },
            {
              header: 'Remark',
              sortValue: (l) => remarkLabel[l.remark].label,
              cell: (l) => <Pill tone={remarkLabel[l.remark].tone}>{remarkLabel[l.remark].label}</Pill>,
            },
          ]}
        />
      )}
    </RecordDetail>
  );
}
