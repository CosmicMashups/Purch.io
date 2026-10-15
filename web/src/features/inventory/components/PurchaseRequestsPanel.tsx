import { useState } from 'react';
import { ConfirmModal } from '../../../components/ConfirmModal';
import { toast } from '../../../components/feedback/toastStore';
import { CompactList, CompactRow } from '../../../components/lists/CompactList';
import { Pill } from '../../../components/lists/QueryList';
import { ErrorState } from '../../../components/ErrorState';
import { RowActionsMenu, type RowAction } from '../../../components/RowActionsMenu';
import { SkeletonList } from '../../../components/Skeleton';
import { userMessage } from '../../../lib/apiError';
import { useSession } from '../../auth/useSession';
import { useApprovePurchaseRequest, useCancelPurchaseRequest, usePurchaseRequests, useRestockSuggestions, useSuppliers } from '../queries';
import { PurchaseRequestStatus, type PurchaseRequest } from '../types';
import { ConvertRequestDialog } from './ConvertRequestDialog';

const statusLabel: Record<PurchaseRequestStatus, { label: string; tone: 'neutral' | 'brand' | 'warn' | 'danger' }> = {
  [PurchaseRequestStatus.Open]: { label: 'Awaiting approval', tone: 'warn' },
  [PurchaseRequestStatus.Approved]: { label: 'Approved', tone: 'brand' },
  [PurchaseRequestStatus.Converted]: { label: 'Ordered', tone: 'brand' },
  [PurchaseRequestStatus.Cancelled]: { label: 'Cancelled', tone: 'neutral' },
};

/** Purchase requests as a compact list; approve, convert and cancel live in each row's menu. */
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

  function actionsFor(request: PurchaseRequest): RowAction[] {
    const list: RowAction[] = [];
    if (request.status === PurchaseRequestStatus.Open && canApprove) list.push({ label: 'Approve', onSelect: () => void onApprove(request) });
    if (request.status === PurchaseRequestStatus.Approved) list.push({ label: 'Create purchase orders', onSelect: () => setConverting(request) });
    if (request.status === PurchaseRequestStatus.Open || request.status === PurchaseRequestStatus.Approved) {
      list.push({ label: 'Cancel request', danger: true, separated: list.length > 0, onSelect: () => setCancelling(request) });
    }
    return list;
  }

  return (
    <section aria-labelledby="purchase-requests-heading" className="flex flex-col gap-3">
      <h2 id="purchase-requests-heading" className="sr-only">
        Purchase requests
      </h2>
      {requests.isPending && <SkeletonList />}
      {requests.isError && <ErrorState title="Purchase requests could not be loaded" message={userMessage(requests.error)} onRetry={() => void requests.refetch()} />}
      {requests.isSuccess && requests.data.length === 0 && (
        <p className="rounded-panel border border-dashed border-ink-soft/40 p-6 text-base text-ink-soft">No purchase requests yet. Start one from Restock first.</p>
      )}
      {requests.isSuccess && requests.data.length > 0 && (
        <CompactList label="Purchase requests">
          {requests.data.map((request) => {
            const status = statusLabel[request.status];
            return (
              <CompactRow
                key={request.id}
                title={request.controlNumber}
                detail={
                  <>
                    <p className="truncate">
                      {request.lines.map((l) => `${l.name} ${l.quantity} ${l.unit}`).join(', ')}
                    </p>
                    <p className="truncate">
                      {request.requestDate} by {request.requestedByName || 'unknown'}
                      {request.dateNeeded ? `, needed by ${request.dateNeeded}` : ''}, to {request.branchName}
                    </p>
                  </>
                }
                aside={<Pill tone={status.tone}>{status.label}</Pill>}
                menu={<RowActionsMenu subject={request.controlNumber} actions={actionsFor(request)} />}
              />
            );
          })}
        </CompactList>
      )}
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
