import { useState } from 'react';
import { ErrorState } from '../../../components/ErrorState';
import { Skeleton } from '../../../components/Skeleton';
import { userMessage } from '../../../lib/apiError';
import { STAFF_ROLES } from '../../../permissions/navPolicy';
import { useApprovalsReview } from '../queries';
import { AuditActionType, type ApproverSummary } from '../types';

const ACTION_LABEL: Record<AuditActionType, string> = {
  [AuditActionType.Void]: 'Void',
  [AuditActionType.Refund]: 'Refund',
};

function todayIso(): string {
  return new Date().toISOString().slice(0, 10);
}

/**
 * Every void, refund and kitchen-order edit needed a manager or admin's PIN — this is the daily review of
 * who approved what, so a store owner can spot a pattern (one approver signing off far more than usual, a
 * run of after-hours approvals, one cashier's requests dominating one approver's day) without reading the
 * raw audit log line by line. See ApprovalsReviewCalculator on the server for exactly what counts as unusual.
 */
export function ApprovalsPanel() {
  const [date, setDate] = useState(todayIso());
  const isToday = date === todayIso();
  const review = useApprovalsReview(isToday ? undefined : date);

  return (
    <div className="flex max-w-3xl flex-col gap-4 print:max-w-none">
      <div className="flex flex-wrap items-end justify-between gap-3 print:hidden">
        <label className="flex flex-col gap-1">
          <span className="text-sm font-semibold text-ink-soft">Date</span>
          <input
            type="date"
            value={date}
            max={todayIso()}
            onChange={(e) => setDate(e.target.value)}
            className="h-12 rounded-control border border-ink-soft/40 bg-surface px-3 text-base"
          />
        </label>
        <button type="button" onClick={() => window.print()} className="h-12 rounded-control border border-line bg-surface px-5 text-base font-semibold hover:border-brand">
          Print
        </button>
      </div>

      {review.isPending && (
        <div className="flex flex-col gap-3" aria-busy="true">
          <Skeleton className="h-6 w-1/3" />
          <Skeleton className="h-40 w-full" />
        </div>
      )}
      {review.isError && <ErrorState title="The approvals review could not be loaded" message={userMessage(review.error)} onRetry={() => void review.refetch()} />}
      {review.isSuccess &&
        (review.data.approvers.length === 0 ? (
          <p className="rounded-panel border border-dashed border-ink-soft/40 p-6 text-base text-ink-soft">
            No void, refund or kitchen-order edit needed approval on {review.data.date}.
          </p>
        ) : (
          <div className="flex flex-col gap-4">
            <p className="text-base text-ink-soft">
              {review.data.totalApprovals} approval{review.data.totalApprovals === 1 ? '' : 's'} on {review.data.date}.
            </p>
            {review.data.approvers.map((approver) => (
              <ApproverCard key={approver.approverId} approver={approver} />
            ))}
          </div>
        ))}
    </div>
  );
}

function ApproverCard({ approver }: { approver: ApproverSummary }) {
  const flagged = approver.flags.length > 0;
  return (
    <section
      aria-label={approver.approverName}
      className={`rounded-panel border p-5 ${flagged ? 'border-warn bg-warn/5' : 'border-line bg-surface'}`}
    >
      <header className="flex flex-wrap items-baseline justify-between gap-2">
        <h3 className="text-lg font-bold">
          {approver.approverName} <span className="text-sm font-normal text-ink-soft">{STAFF_ROLES[approver.approverRole] ?? ''}</span>
        </h3>
        <p className="text-base font-semibold tabular-nums">{approver.totalApprovals} approval{approver.totalApprovals === 1 ? '' : 's'}</p>
      </header>

      {flagged && (
        <ul className="mt-3 flex flex-col gap-1">
          {approver.flags.map((flag) => (
            <li key={flag.message} className="flex items-start gap-2 text-base font-semibold text-warn">
              <span aria-hidden="true">⚠</span>
              {flag.message}
            </li>
          ))}
        </ul>
      )}

      <table className="mt-4 w-full text-left text-sm">
        <thead>
          <tr className="text-ink-soft">
            <th className="pb-1 pr-3 font-semibold">Time</th>
            <th className="pb-1 pr-3 font-semibold">Action</th>
            <th className="pb-1 font-semibold">Requested by</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-line">
          {approver.entries.map((entry) => (
            <tr key={`${entry.targetEntityId}-${entry.createdAt}`}>
              <td className="py-1.5 pr-3 tabular-nums">
                {new Date(entry.createdAt).toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' })}
                {entry.afterHours && <span className="ml-1 text-warn">(after hours)</span>}
              </td>
              <td className="py-1.5 pr-3">{ACTION_LABEL[entry.actionType] ?? 'Action'}</td>
              <td className="py-1.5">{entry.requesterName}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </section>
  );
}
