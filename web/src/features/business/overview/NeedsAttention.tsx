import { CaretRight, WarningCircle, XCircle } from '@phosphor-icons/react';
import { Link } from 'react-router-dom';
import { AllClear } from '../../../components/feedback/AllClear';
import { Panel } from '../../../components/Panel';
import { Skeleton } from '../../../components/Skeleton';
import { useCreditReminders } from '../../credit/queries';
import { useFlaggedSync, useInventoryDashboard } from '../../dashboard/queries';

interface AttentionRow {
  key: string;
  tone: 'warn' | 'danger';
  title: string;
  detail: string;
  to: string;
}

const count = (n: number, one: string, many: string) => `${n} ${n === 1 ? one : many}`;

/** What to look at first, gathered from the places that already know: sync review, credit due dates and stock. */
export function NeedsAttention() {
  const flagged = useFlaggedSync(true);
  const reminders = useCreditReminders(7);
  const stock = useInventoryDashboard(true);

  const queries = [flagged, reminders, stock];
  if (queries.every((q) => q.isPending)) {
    return (
      <Panel title="Needs attention">
        <Skeleton className="h-16 w-full" />
      </Panel>
    );
  }

  const rows: AttentionRow[] = [];
  const open = flagged.data?.filter((r) => r.reviewedAt === null).length ?? 0;
  if (open > 0) {
    rows.push({ key: 'sync', tone: 'warn', title: `${count(open, 'offline record needs', 'offline records need')} review`, detail: 'Two devices changed the same record.', to: '/business/sync-conflicts' });
  }
  const overdue = reminders.data?.filter((r) => r.isOverdue).length ?? 0;
  const soon = (reminders.data?.length ?? 0) - overdue;
  if (overdue > 0) {
    rows.push({ key: 'overdue', tone: 'danger', title: `${count(overdue, 'customer account is', 'customer accounts are')} overdue`, detail: 'Utang past its due date.', to: '/business/customers' });
  }
  if (soon > 0) {
    rows.push({ key: 'soon', tone: 'warn', title: `${count(soon, 'account falls', 'accounts fall')} due within 7 days`, detail: 'A reminder before it becomes overdue.', to: '/business/customers' });
  }
  const out = stock.data?.outOfStockCount ?? 0;
  if (out > 0) {
    rows.push({ key: 'out', tone: 'danger', title: `${count(out, 'item is', 'items are')} out of stock`, detail: 'These cannot be sold until they are restocked.', to: '/inventory' });
  }
  const low = stock.data?.lowStockCount ?? 0;
  if (low > 0) {
    rows.push({ key: 'low', tone: 'warn', title: `${count(low, 'item is', 'items are')} running low`, detail: 'At or under the level you set to be warned at.', to: '/inventory' });
  }

  const failed = queries.some((q) => q.isError);

  return (
    <Panel title="Needs attention" subtitle="The first things worth a look today">
      {rows.length === 0 ? (
        <AllClear>Nothing needs attention right now.</AllClear>
      ) : (
        <ul className="divide-y divide-line">
          {rows.map((row) => {
            const Icon = row.tone === 'danger' ? XCircle : WarningCircle;
            return (
              <li key={row.key}>
                <Link to={row.to} className="-mx-2 flex min-h-16 items-center gap-4 rounded-control px-2 py-3 hover:bg-canvas">
                  <Icon size={26} weight="fill" aria-hidden="true" className={row.tone === 'danger' ? 'shrink-0 text-danger' : 'shrink-0 text-warn'} />
                  <span className="min-w-0 flex-1">
                    <span className="block text-base font-semibold">{row.title}</span>
                    <span className="block text-sm text-ink-soft">{row.detail}</span>
                  </span>
                  <CaretRight size={20} aria-hidden="true" className="shrink-0 text-ink-soft" />
                </Link>
              </li>
            );
          })}
        </ul>
      )}
      {failed && (
        <p role="status" className="mt-3 text-sm text-ink-soft">
          Some checks could not be loaded, so this list may be incomplete.{' '}
          <button type="button" onClick={() => queries.forEach((q) => void q.refetch())} className="font-semibold text-brand-strong underline">
            Try again
          </button>
        </p>
      )}
    </Panel>
  );
}
