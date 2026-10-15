import { useRef, useState } from 'react';
import { useParams } from 'react-router-dom';
import { ApproverPinDialog } from '../../../components/ApproverPinDialog';
import { ConfirmModal } from '../../../components/ConfirmModal';
import { formatPeso } from '../../dashboard/format';
import { ErrorState } from '../../../components/ErrorState';
import { toast } from '../../../components/feedback/toastStore';
import { PrimaryButton, SecondaryButton } from '../../../components/forms/FormField';
import { Pill } from '../../../components/lists/QueryList';
import { PageHeader } from '../../../components/PageHeader';
import { SkeletonList } from '../../../components/Skeleton';
import { ApiError, userMessage } from '../../../lib/apiError';
import { useSession } from '../../auth/useSession';
import { useApproverPinAction } from '../../shared/useApproverPinAction';
import { StockTakeCountList } from '../components/StockTakeCountList';
import { useApplyStockTake, useCountStockTakeLine, useStockTake, useStockTakeTransition } from '../queries';
import { stockTakeStatusLabel } from '../stockTake';
import { StockTakeLineState, StockTakeStatus, type StockTakeLine } from '../types';

const CHANGED = 'This stock take changed. Reloading the latest version.';

export function StockTakeDetailPage() {
  const { id = '' } = useParams();
  const { role } = useSession();
  const canReview = role === 'Admin' || role === 'Manager';
  const take = useStockTake(id);
  const count = useCountStockTakeLine(id);
  const start = useStockTakeTransition(id, 'start');
  const review = useStockTakeTransition(id, 'review');
  const cancel = useStockTakeTransition(id, 'cancel');
  const apply = useApplyStockTake(id);
  const [confirmCancel, setConfirmCancel] = useState(false);
  const applying = useRef<Promise<unknown> | null>(null);

  const describe = (error: unknown) => {
    if (error instanceof ApiError && error.kind === 'conflict') {
      void take.refetch();
      return CHANGED;
    }
    return userMessage(error);
  };

  const pin = useApproverPinAction({
    // In-flight guard: a second submit while one is sending reuses the same request.
    send: (p) => {
      if (applying.current) return applying.current as Promise<unknown>;
      const request = apply.mutateAsync(p).finally(() => (applying.current = null));
      applying.current = request;
      return request;
    },
    onDone: () => toast.success('Stock take applied'),
    describeError: describe,
  });

  async function run(action: () => Promise<unknown>) {
    try {
      await action();
    } catch (error) {
      toast.error(describe(error));
    }
  }

  if (take.isPending) return <SkeletonList />;
  if (take.isError || !take.data) {
    return <ErrorState title="Stock take could not be loaded" message={userMessage(take.error)} onRetry={() => void take.refetch()} />;
  }

  const t = take.data;
  const label = stockTakeStatusLabel[t.status];
  const busy = start.isPending || review.isPending || cancel.isPending || apply.isPending;
  const open = t.status === StockTakeStatus.Draft || t.status === StockTakeStatus.Counting || t.status === StockTakeStatus.Review;

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title={t.name}
        subtitle={`${t.countedCount} counted, ${t.uncountedCount} not counted, ${t.skippedCount} skipped`}
        backTo={{ to: '/inventory/stock-takes', label: 'Stock takes' }}
        action={
          <div className="flex flex-wrap items-center gap-2">
            {t.blind && <Pill>Blind</Pill>}
            <Pill tone={label.tone}>{label.label}</Pill>
            {t.status === StockTakeStatus.Draft && (
              <PrimaryButton type="button" busy={busy} onClick={() => void run(() => start.mutateAsync())}>
                Start counting
              </PrimaryButton>
            )}
            {t.status === StockTakeStatus.Counting && canReview && (
              <PrimaryButton type="button" busy={busy} onClick={() => void run(() => review.mutateAsync())}>
                Move to review
              </PrimaryButton>
            )}
            {t.status === StockTakeStatus.Review && canReview && (
              <PrimaryButton type="button" busy={busy} onClick={() => void pin.run('Apply stock take', 'Enter a manager or admin PIN to apply these counts.')}>
                Apply counts
              </PrimaryButton>
            )}
            {open && (
              <SecondaryButton type="button" disabled={busy} onClick={() => setConfirmCancel(true)}>
                Cancel take
              </SecondaryButton>
            )}
          </div>
        }
      />

      {t.warnings.length > 0 && (
        <ul className="rounded-panel border border-line bg-surface p-4 text-base text-ink-soft">
          {t.warnings.map((w) => (
            <li key={w}>{w}</li>
          ))}
        </ul>
      )}

      {t.status === StockTakeStatus.Counting && (
        <StockTakeCountList
          lines={t.lines}
          busy={count.isPending}
          onCount={(lineId, counted, skip) => {
            if (count.isPending) return;
            void run(() => count.mutateAsync({ lineId, counted, skip }));
          }}
        />
      )}
      {t.status === StockTakeStatus.Review && <ReviewLines lines={t.lines} />}
      {t.status === StockTakeStatus.Review && canReview && (
        <section aria-label="Recount" className="flex flex-col gap-3">
          <h2 className="text-lg font-bold">Recount a line</h2>
          <StockTakeCountList
            lines={[...t.lines].sort((a, b) => Number(!!b.needsRecount) - Number(!!a.needsRecount))}
            busy={count.isPending}
            onCount={(lineId, counted, skip) => {
              if (count.isPending) return;
              void run(() => count.mutateAsync({ lineId, counted, skip }));
            }}
          />
        </section>
      )}
      {(t.status === StockTakeStatus.Applied || t.status === StockTakeStatus.Cancelled || t.status === StockTakeStatus.Draft) && (
        <ResultLines lines={t.lines} hideExpected={t.blind && t.status === StockTakeStatus.Draft} />
      )}

      <ConfirmModal
        open={confirmCancel}
        destructive
        busy={cancel.isPending}
        title="Cancel this stock take?"
        description="The counted lines will be discarded and no stock will change."
        confirmLabel="Cancel take"
        onConfirm={() => void run(() => cancel.mutateAsync()).finally(() => setConfirmCancel(false))}
        onCancel={() => setConfirmCancel(false)}
      />
      {pin.dialogProps && <ApproverPinDialog open {...pin.dialogProps} />}
    </div>
  );
}

function ReviewLines({ lines }: { lines: StockTakeLine[] }) {
  const uncounted = lines.filter((l) => l.state === StockTakeLineState.Uncounted);
  const rest = lines.filter((l) => l.state !== StockTakeLineState.Uncounted);
  return (
    <div className="flex flex-col gap-6">
      {uncounted.length > 0 && (
        <section aria-label="Not counted" className="rounded-panel border-2 border-danger bg-surface p-4">
          <h2 className="text-lg font-bold text-danger">{uncounted.length} not counted</h2>
          <p className="text-base text-ink-soft">These keep their current stock when applied.</p>
          <ul className="mt-2">
            {uncounted.map((l) => (
              <li key={l.id} className="flex min-h-12 items-center text-base font-semibold">
                {l.name}
              </li>
            ))}
          </ul>
        </section>
      )}
      <ResultLines lines={rest} />
    </div>
  );
}

const signed = (n: number) => (n > 0 ? `+${n}` : String(n));

function ResultLines({ lines, hideExpected = false }: { lines: StockTakeLine[]; hideExpected?: boolean }) {
  const showValue = lines.some((l) => l.varianceValue !== null);
  return (
    <div className="overflow-x-auto rounded-panel border border-line bg-surface">
      <table className="w-full text-left text-base">
        <thead>
          <tr className="border-b border-line text-sm text-ink-soft">
            <th className="p-3">Item</th>
            {!hideExpected && <th className="p-3 text-right">Expected</th>}
            <th className="p-3 text-right">Counted</th>
            <th className="p-3 text-right">Variance</th>
            {showValue && <th className="p-3 text-right">Value impact</th>}
          </tr>
        </thead>
        <tbody>
          {lines.map((l) => (
            <tr key={l.id} className="border-b border-line last:border-0">
              <td className="p-3 font-medium">
                {l.name}
                {l.state === StockTakeLineState.Skipped && <span className="ml-2 text-sm text-ink-soft">Skipped</span>}
                {l.needsRecount && <span className="ml-2 text-sm font-semibold text-danger">Needs recount</span>}
              </td>
              {!hideExpected && <td className="p-3 text-right tabular-nums">{l.expectedQty ?? ''}</td>}
              <td className="p-3 text-right tabular-nums">{l.countedQty ?? ''}</td>
              <td className="p-3 text-right tabular-nums">{l.varianceQty === null ? '' : signed(l.varianceQty)}</td>
              {showValue && <td className="p-3 text-right tabular-nums">{l.varianceValue === null ? '' : formatPeso(l.varianceValue)}</td>}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
