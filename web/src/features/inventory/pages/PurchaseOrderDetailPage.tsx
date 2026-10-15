import { Link, useParams } from 'react-router-dom';
import { Pill } from '../../../components/lists/QueryList';
import { formatPeso } from '../../dashboard/format';
import { printPurchaseOrder } from '../printPurchaseOrder';
import { usePurchaseOrders } from '../queries';
import type { PurchaseOrderLine } from '../types';
import { isPurchaseOrderOpen, purchaseOrderStatusLabel } from '../workflow';
import { LinesTable, RecordDetail } from './RecordDetail';

const button = 'inline-flex h-12 items-center rounded-control border border-line px-5 text-base font-semibold hover:border-brand';

export function PurchaseOrderDetailPage() {
  const { id } = useParams();
  const orders = usePurchaseOrders();

  return (
    <RecordDetail
      query={orders}
      id={id}
      notFound="This purchase order no longer exists"
      backTo={{ to: '/inventory/purchase-orders', label: 'Purchase orders' }}
      title={(o) => o.controlNumber || `Order to ${o.supplierName}`}
      subtitle={(o) => `${o.supplierName}, deliver to ${o.branchName}`}
      action={(o) => (
        <div className="flex flex-wrap gap-2">
          <button type="button" className={button} onClick={() => printPurchaseOrder(o)}>
            Print
          </button>
          {isPurchaseOrderOpen(o.status) && (
            <Link to={`/inventory/incoming-receiving?po=${o.id}`} className={`${button} border-brand bg-brand text-on-brand`}>
              Record delivery
            </Link>
          )}
        </div>
      )}
      facts={(o) => [
        { label: 'Status', value: <Pill tone={purchaseOrderStatusLabel[o.status].tone}>{purchaseOrderStatusLabel[o.status].label}</Pill> },
        { label: 'Supplier', value: o.supplierName },
        { label: 'Deliver to', value: o.branchName },
        { label: 'Delivery address', value: o.deliveryAddress },
        { label: 'Date requested', value: o.requestDate },
        { label: 'Needed by', value: o.dateNeeded },
        { label: 'Requested by', value: o.requestedByName },
        { label: 'Pay to', value: o.paymentAccountNumber ? `${o.paymentAccountName ? `${o.paymentAccountName}, ` : ''}${o.paymentAccountNumber}` : null },
        {
          label: 'Deliveries',
          value:
            o.receipts.length > 0
              ? `${o.receipts.length} ${o.receipts.length === 1 ? 'report' : 'reports'}, last on ${o.receipts[o.receipts.length - 1].deliveryDate}`
              : null,
        },
        { label: 'Remarks', value: o.remarks },
      ]}
    >
      {(o) => (
        <LinesTable<PurchaseOrderLine>
          label="Items ordered"
          lines={o.lines}
          getId={(l) => l.id}
          nameOf={(l) => l.itemName}
          columns={[
            { header: 'Item', sortValue: (l) => l.itemName, cell: (l) => <span className="font-medium text-gray-900">{l.itemName}</span> },
            { header: 'Ordered', className: 'text-right tabular-nums', sortValue: (l) => l.quantityOrdered, cell: (l) => `${l.quantityOrdered} ${l.unit ?? ''}` },
            { header: 'Delivered', className: 'text-right tabular-nums', sortValue: (l) => l.quantityReceived, cell: (l) => `${l.quantityReceived} ${l.unit ?? ''}` },
            { header: 'Unit cost', className: 'text-right tabular-nums', sortValue: (l) => l.expectedUnitCost, cell: (l) => formatPeso(l.expectedUnitCost) },
            { header: 'Amount', className: 'text-right tabular-nums', sortValue: (l) => l.amount ?? l.quantityOrdered * l.expectedUnitCost, cell: (l) => formatPeso(l.amount ?? l.quantityOrdered * l.expectedUnitCost) },
          ]}
          footer={
            o.orderTotal !== undefined && o.orderTotal > 0 ? (
              <dl className="ml-auto grid w-full max-w-xs grid-cols-[1fr_auto] gap-x-4 gap-y-1 text-sm tabular-nums">
                <dt className="text-ink-soft">Subtotal</dt>
                <dd className="text-right">{formatPeso(o.subtotal ?? 0)}</dd>
                {(o.discountAmount ?? 0) > 0 && (
                  <>
                    <dt className="text-ink-soft">Less discount</dt>
                    <dd className="text-right">{formatPeso(o.discountAmount ?? 0)}</dd>
                  </>
                )}
                <dt className="text-ink-soft">VAT {o.vatInclusive ? '(included)' : '(added)'}</dt>
                <dd className="text-right">{formatPeso(o.vatAmount ?? 0)}</dd>
                <dt className="font-semibold">Order total</dt>
                <dd className="text-right font-semibold">{formatPeso(o.orderTotal)}</dd>
              </dl>
            ) : undefined
          }
        />
      )}
    </RecordDetail>
  );
}
