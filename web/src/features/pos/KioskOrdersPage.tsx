import { useNavigate } from 'react-router-dom';
import { ErrorState } from '../../components/ErrorState';
import { SkeletonList } from '../../components/Skeleton';
import { toast } from '../../components/feedback/toastStore';
import { PageHeader } from '../../components/PageHeader';
import { SecondaryButton } from '../../components/forms/FormField';
import { userMessage } from '../../lib/apiError';
import { useSession } from '../auth/useSession';
import { formatPeso } from '../dashboard/format';
import { DeviceRequired } from './components/DeviceRequired';
import { useClaimKioskOrder, usePendingKioskOrders } from './queries';
import type { Transaction } from './types';

export function KioskOrdersPage() {
  const { claims } = useSession();
  if (!claims?.deviceId || !claims.branchId) return <DeviceRequired />;
  return <PendingOrders branchId={claims.branchId} />;
}

function summarize(order: Transaction): string {
  return order.lines.map((line) => `${line.quantity} x ${line.itemName}`).join(', ');
}

function PendingOrders({ branchId }: { branchId: string }) {
  const navigate = useNavigate();
  const orders = usePendingKioskOrders(branchId);
  const claim = useClaimKioskOrder();

  function take(order: Transaction) {
    claim.mutate(order.id, {
      onSuccess: () => {
        toast.success(order.kioskPrepNumber ? `Order ${order.kioskPrepNumber} is now your cart` : 'The order is now your cart');
        navigate('/sell');
      },
    });
  }

  return (
    <div className="flex max-w-3xl flex-col gap-6">
      <PageHeader
        title="Kiosk orders"
        subtitle="Orders customers placed at the kiosk, waiting for payment"
        backTo={{ to: '/sell', label: 'Cashier' }}
        action={
          <SecondaryButton type="button" disabled={orders.isFetching} onClick={() => void orders.refetch()}>
            {orders.isFetching ? 'Refreshing...' : 'Refresh'}
          </SecondaryButton>
        }
      />

      {orders.isPending && <SkeletonList rows={3} />}
      {orders.isError && <ErrorState title="Kiosk orders could not be loaded" message={userMessage(orders.error)} onRetry={() => void orders.refetch()} />}
      {orders.isSuccess && orders.data.length === 0 && (
        <p className="rounded-panel border border-dashed border-ink-soft/40 p-8 text-center text-base text-ink-soft">No kiosk orders are waiting. This list refreshes by itself.</p>
      )}
      {orders.isSuccess && orders.data.length > 0 && (
        <ul className="flex flex-col gap-3">
          {orders.data.map((order) => (
            <li key={order.id} className="flex flex-wrap items-center justify-between gap-4 rounded-panel border border-line bg-surface p-5">
              <div className="min-w-0 flex-1">
                <p className="text-2xl font-bold tabular-nums">{order.kioskPrepNumber !== null ? `Order ${order.kioskPrepNumber}` : 'Kiosk order'}</p>
                <p className="text-base">{summarize(order)}</p>
                <p className="text-sm text-ink-soft">{order.orderType ? `${order.orderType}, ` : ''}total {formatPeso(order.totalAmount)}</p>
              </div>
              <button
                type="button"
                disabled={claim.isPending}
                onClick={() => take(order)}
                className="h-14 rounded-control bg-brand px-6 text-lg font-bold text-on-brand hover:bg-brand-strong active:translate-y-px disabled:opacity-60"
              >
                Take this order
              </button>
            </li>
          ))}
        </ul>
      )}

      <p className="text-sm text-ink-soft">
        Taking an order makes it your cart so you can add the payment. If you already have items in your cart, finish that sale first. A manager can clear the cart from the Cashier screen.
      </p>
    </div>
  );
}
