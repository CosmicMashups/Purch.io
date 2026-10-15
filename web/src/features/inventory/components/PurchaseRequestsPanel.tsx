import { useState } from 'react';
import { ConfirmModal } from '../../../components/ConfirmModal';
import { toast } from '../../../components/feedback/toastStore';
import { ListCard, Pill, QueryList } from '../../../components/lists/QueryList';
import { useSession } from '../../auth/useSession';
import { useApprovePurchaseRequest, useCancelPurchaseRequest, usePurchaseRequests, useRestockSuggestions, useSuppliers } from '../queries';
import { PurchaseRequestStatus, type PurchaseRequest } from '../types';
import { ConvertRequestDialog } from './ConvertRequestDialog';

const actionButton = 'h-12 rounded-control border border-line px-5 text-base font-semibold hover:border-brand disabled:opacity-60';

const statusLabel: Record<PurchaseRequestStatus, { label: string; tone: 'neutral' | 'brand' | 'warn' | 'danger' }> = {
  [PurchaseRequestStatus.Open]: { label: 'Awaiting approval', tone: 'warn' },
  [PurchaseRequestStatus.Approved]: { label: 'Approved', tone: 'brand' },
  [PurchaseRequestStatus.Converted]: { label: 'Ordered', tone: 'brand' },
  [PurchaseRequestStatus.Cancelled]: { label: 'Cancelled', tone: 'neutral' },
};

/** Restock needs that have not become orders yet: approve, then turn them into purchase orders per supplier. */
export function PurchaseRequestsPanel() {
  const requests = usePurchaseRequests();
  const suppliers = useSuppliers();
  const suggestions = useRestockSuggestions();
  const approve = useApprovePurchaseRequest();
  const cancel = useCancelPurchaseRequest();
  const { role } = useSession();
  const canApprove = role === 'Admin' || role === 'Manager';
  const [converting, setConverting] = useState<PurchaseRequest | null>(null);
  const [cancelling, setCancelling] = useState<PurchaseRequest | null>(null);

  async function onApprove(request: PurchaseRequest) {
    await approve.mutateAsync(request.id);
    toast.success(`${request.controlNumber} approved`);
  }

  async function onConfirmCancel() {
    if (!cancelling) return;
    const request = cancelling;
    try {
      await cancel.mutateAsync(request.id);
      toast.success(`${request.controlNumber} cancelled`);
    } finally {
      setCancelling(null);
    }
  }

  return (
    <section aria-labelledby="purchase-requests-heading" className="flex flex-col gap-3">
      <h2 id="purchase-requests-heading" className="text-lg font-semibold">
        Purchase requests
      </h2>
      <QueryList
        query={requests}
        errorTitle="Purchase requests could not be loaded"
        emptyMessage="No purchase requests yet. Start one from Restock first."
        renderRow={(request) => {
          const status = statusLabel[request.status];
          const open = request.status === PurchaseRequestStatus.Open;
          const approved = request.status === PurchaseRequestStatus.Approved;
          return (
            <ListCard key={request.id}>
              <div className="min-w-0 flex-1">
                <p className="text-base font-semibold">{request.controlNumber}</p>
                <p className="text-sm text-ink-soft">
                  Requested {request.requestDate} by {request.requestedByName || 'unknown'}
                  {request.dateNeeded ? `, needed by ${request.dateNeeded}` : ''}
                </p>
                <p className="text-sm text-ink-soft">
                  Deliver to {request.branchName}
                  {request.deliveryAddress ? `, ${request.deliveryAddress}` : ''}
                </p>
                <ul className="mt-2 flex flex-col gap-1">
                  {request.lines.map((line) => (
                    <li key={line.id} className="text-base tabular-nums">
                      {line.name}: {line.quantity} {line.unit}
                    </li>
                  ))}
                </ul>
                {request.remarks && <p className="mt-2 text-sm text-ink-soft">{request.remarks}</p>}
                {(open || approved) && (
                  <div className="mt-3 flex flex-wrap gap-2">
                    {open && canApprove && (
                      <button type="button" className={actionButton} disabled={approve.isPending} onClick={() => void onApprove(request)}>
                        Approve
                      </button>
                    )}
                    {approved && (
                      <button type="button" className={actionButton} onClick={() => setConverting(request)}>
                        Create purchase orders
                      </button>
                    )}
                    <button type="button" className={`${actionButton} text-danger`} onClick={() => setCancelling(request)}>
                      Cancel request
                    </button>
                  </div>
                )}
              </div>
              <Pill tone={status.tone}>{status.label}</Pill>
            </ListCard>
          );
        }}
      />
      {converting && suppliers.data && (
        <ConvertRequestDialog request={converting} suppliers={suppliers.data} suggestions={suggestions.data ?? []} onClose={() => setConverting(null)} />
      )}
      <ConfirmModal
        open={cancelling !== null}
        destructive
        busy={cancel.isPending}
        title="Cancel this purchase request?"
        description={cancelling ? `${cancelling.controlNumber} will be cancelled and its items can be requested again.` : undefined}
        confirmLabel="Cancel request"
        onConfirm={() => void onConfirmCancel()}
        onCancel={() => setCancelling(null)}
      />
    </section>
  );
}
