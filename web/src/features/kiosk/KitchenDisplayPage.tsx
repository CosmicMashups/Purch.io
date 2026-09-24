import { useState } from 'react';
import { ErrorState } from '../../components/ErrorState';
import { Skeleton } from '../../components/Skeleton';
import { userMessage } from '../../lib/apiError';
import { useSession } from '../auth/useSession';
import { FullscreenButton } from '../../hardware/fullscreen';
import { KitchenStatus, type Transaction } from '../pos/types';
import { useDisplayOrders, useSetKitchenStatus } from './queries';
import { ResetDeviceDialog } from './ResetDeviceDialog';
import { actionLabel, byPrepNumber, lineDetails, nextKitchenStatus, statusLabel } from './tickets';

const STATUS_STYLE: Record<KitchenStatus, string> = {
  [KitchenStatus.Queued]: 'bg-slate-100 text-slate-800',
  [KitchenStatus.Preparing]: 'bg-amber-100 text-amber-900',
  [KitchenStatus.Ready]: 'bg-emerald-100 text-emerald-900',
  [KitchenStatus.PickedUp]: 'bg-slate-100 text-slate-600',
};

export function KitchenDisplayPage() {
  const { claims } = useSession();
  const branchId = claims?.branchId ?? null;
  const orders = useDisplayOrders('KitchenDisplay', branchId);
  const advance = useSetKitchenStatus(branchId);
  const [resetting, setResetting] = useState(false);
  const tickets = byPrepNumber(orders.data ?? []);

  return (
    <div className="mx-auto flex min-h-dvh max-w-7xl flex-col gap-4 bg-canvas p-4 text-ink">
      <header className="flex items-center justify-between gap-4">
        <h1 className="text-3xl font-bold tracking-tight">Kitchen tickets</h1>
        <div className="flex gap-2">
          <FullscreenButton />
          <button type="button" onClick={() => setResetting(true)} className="h-12 rounded-control border border-line px-4 text-base font-semibold">
            Unpair this screen
          </button>
        </div>
      </header>

      {!branchId && <ErrorState title="This screen has no branch" message="Pair it again with a kitchen display device that belongs to a branch." />}
      {orders.isPending && branchId && <Skeleton className="h-48 w-full" />}
      {orders.isError && !orders.data && <ErrorState title="Orders could not be loaded" message={userMessage(orders.error)} onRetry={() => void orders.refetch()} />}
      {orders.isError && orders.data && (
        <p role="status" className="rounded-control border border-warn/40 bg-warn/10 px-3 py-2 text-base font-medium">
          Cannot reach the server. Showing the last orders received. Trying again every few seconds.
        </p>
      )}
      {orders.isSuccess && tickets.length === 0 && <p className="rounded-panel border border-dashed border-ink-soft/40 p-12 text-center text-2xl text-ink-soft">No pending orders</p>}

      <ul className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
        {tickets.map((order) => (
          <Ticket key={order.id} order={order} busy={advance.isPending} onAdvance={(status) => advance.mutate({ transactionId: order.id, status })} />
        ))}
      </ul>

      <ResetDeviceDialog open={resetting} onClose={() => setResetting(false)} />
    </div>
  );
}

function Ticket({ order, busy, onAdvance }: { order: Transaction; busy: boolean; onAdvance: (status: KitchenStatus) => void }) {
  const next = nextKitchenStatus(order.kitchenStatus);
  const label = actionLabel(order.kitchenStatus);
  return (
    <li className={`flex flex-col gap-3 rounded-panel border bg-surface p-4 ${order.kitchenStatus === KitchenStatus.Ready ? 'border-2 border-ok' : 'border-line'}`}>
      <div className="flex items-start justify-between gap-3">
        <div>
          <p className="text-4xl font-black tabular-nums">#{order.kioskPrepNumber ?? '—'}</p>
          {order.orderType && <p className="text-base font-semibold text-ink-soft">{order.orderType}</p>}
        </div>
        <span className={`rounded-full px-3 py-1 text-sm font-bold ${STATUS_STYLE[order.kitchenStatus]}`}>{statusLabel(order.kitchenStatus)}</span>
      </div>
      <ul className="flex flex-col gap-2">
        {order.lines.map((line) => {
          const details = lineDetails(line);
          return (
            <li key={line.id}>
              <p className="text-xl font-semibold">
                {line.quantity} × {line.itemName}
              </p>
              {details.length > 0 && <p className="text-base text-ink-soft">{details.join(' · ')}</p>}
            </li>
          );
        })}
      </ul>
      {next !== null && label && (
        <button type="button" disabled={busy} onClick={() => onAdvance(next)} className="mt-auto h-16 rounded-control bg-brand text-xl font-bold text-on-brand disabled:opacity-60">
          {label}
        </button>
      )}
    </li>
  );
}
