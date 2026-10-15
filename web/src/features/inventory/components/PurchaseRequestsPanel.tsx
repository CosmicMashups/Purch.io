import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ConfirmModal } from '../../../components/ConfirmModal';
import { toast } from '../../../components/feedback/toastStore';
import { DataTable, type DataColumn } from '../../../components/lists/DataTable';
import { Pill } from '../../../components/lists/QueryList';
import { ErrorState } from '../../../components/ErrorState';
import { RowActionsMenu, type RowAction } from '../../../components/RowActionsMenu';
import { SkeletonList } from '../../../components/Skeleton';
import { userMessage } from '../../../lib/apiError';
import { useSession } from '../../auth/useSession';
import { useApprovePurchaseRequest, useCancelPurchaseRequest, usePurchaseRequests, useRestockSuggestions, useSuppliers } from '../queries';
import { PurchaseRequestStatus, type PurchaseRequest } from '../types';
import { purchaseRequestStatusLabel } from '../workflow';
import { ConvertRequestDialog } from './ConvertRequestDialog';

/** Purchase requests as a compact list; approve, convert and cancel live in each row's menu. */
export function PurchaseRequestsPanel() {
  const navigate = useNavigate();
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

  const columns: DataColumn<PurchaseRequest>[] = [
    { header: 'Request', sortValue: (r) => r.controlNumber, cell: (r) => <span className="font-medium text-gray-900">{r.controlNumber}</span> },
    {
      header: 'Items',
      className: 'text-gray-600',
      sortValue: (r) => r.lines.length,
      cell: (r) => `${r.lines.length} ${r.lines.length === 1 ? 'item' : 'items'}`,
    },
    { header: 'Branch', className: 'text-gray-600', sortValue: (r) => r.branchName, cell: (r) => r.branchName },
    { header: 'Requested', className: 'whitespace-nowrap text-gray-600', sortValue: (r) => r.requestDate, cell: (r) => `${r.requestDate} by ${r.requestedByName || 'unknown'}` },
    { header: 'Needed by', className: 'whitespace-nowrap text-gray-600', sortValue: (r) => r.dateNeeded, cell: (r) => r.dateNeeded ?? '' },
    {
      header: 'Status',
      sortValue: (r) => purchaseRequestStatusLabel[r.status].label,
      cell: (r) => <Pill tone={purchaseRequestStatusLabel[r.status].tone}>{purchaseRequestStatusLabel[r.status].label}</Pill>,
    },
    {
      header: '',
      className: 'text-right',
      cell: (r) => (
        <div className="flex justify-end">
          <RowActionsMenu subject={r.controlNumber} actions={actionsFor(r)} />
        </div>
      ),
    },
  ];

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
        <DataTable
          label="Purchase requests"
          rows={requests.data}
          columns={columns}
          getId={(r) => r.id}
          rowLabel={(r) => `Open ${r.controlNumber}`}
          onRowClick={(r) => navigate(`/inventory/purchase-requests/${r.id}`)}
        />
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
