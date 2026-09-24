import { useState } from 'react';
import { ErrorState } from '../../components/ErrorState';
import { userMessage } from '../../lib/apiError';
import { useSession } from '../auth/useSession';
import type { Transaction } from '../pos/types';
import { useDisplayOrders } from './queries';
import { ResetDeviceDialog } from './ResetDeviceDialog';
import { splitBoard } from './tickets';
import { useLongPress } from './useLongPress';

/** Customer-facing: order numbers only, so it never shows what anyone ordered. */
export function OrderBoardPage() {
  const { claims } = useSession();
  const branchId = claims?.branchId ?? null;
  const orders = useDisplayOrders('OrderBoard', branchId);
  const [resetting, setResetting] = useState(false);
  const hold = useLongPress(() => setResetting(true));
  const { ready, preparing } = splitBoard(orders.data ?? []);

  return (
    <div className="mx-auto flex min-h-dvh max-w-7xl flex-col gap-6 bg-canvas p-6 text-ink">
      <h1 {...hold} className="select-none text-4xl font-extrabold tracking-tight">
        Order status
      </h1>

      {!branchId && <ErrorState title="This screen has no branch" message="Pair it again with an order board device that belongs to a branch." />}
      {orders.isError && !orders.data && <ErrorState title="Orders could not be loaded" message={userMessage(orders.error)} onRetry={() => void orders.refetch()} />}
      {orders.isError && orders.data && (
        <p role="status" className="rounded-control border border-warn/40 bg-warn/10 px-3 py-2 text-lg font-medium">
          Connection lost. These numbers may be out of date.
        </p>
      )}

      {orders.isSuccess && ready.length === 0 && preparing.length === 0 && <p className="py-24 text-center text-3xl text-ink-soft">No pending orders</p>}

      <div className="grid flex-1 gap-8 lg:grid-cols-2">
        <Section title="Ready for pickup" tone="ready" orders={ready} />
        <Section title="Preparing" tone="preparing" orders={preparing} />
      </div>

      <ResetDeviceDialog open={resetting} onClose={() => setResetting(false)} />
    </div>
  );
}

function Section({ title, tone, orders }: { title: string; tone: 'ready' | 'preparing'; orders: Transaction[] }) {
  if (orders.length === 0) return null;
  return (
    <section aria-label={title} aria-live={tone === 'ready' ? 'polite' : undefined} className="flex flex-col gap-4">
      <h2 className={`text-2xl font-bold ${tone === 'ready' ? 'text-ok' : 'text-ink-soft'}`}>{title}</h2>
      <ul className="grid grid-cols-2 gap-4 sm:grid-cols-3">
        {orders.map((order) => (
          <li
            key={order.id}
            className={`grid place-items-center rounded-panel py-8 text-6xl font-black tabular-nums ${tone === 'ready' ? 'bg-ok text-white' : 'border border-line bg-surface text-ink'}`}
          >
            {order.kioskPrepNumber ?? '—'}
          </li>
        ))}
      </ul>
    </section>
  );
}
