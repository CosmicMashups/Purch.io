import { Skeleton } from '../../../components/Skeleton';
import { ErrorState } from '../../../components/ErrorState';
import { userMessage } from '../../../lib/apiError';
import { formatPeso } from '../format';
import { useSalesDashboard } from '../queries';

export function RevenueCards() {
  const query = useSalesDashboard(true);

  if (query.isError) {
    return <ErrorState title="Revenue totals are unavailable" message={userMessage(query.error)} onRetry={() => void query.refetch()} />;
  }

  const cards = [
    { label: 'Today', amount: query.data?.revenueToday, primary: true },
    { label: 'Last 7 days', amount: query.data?.revenueLast7Days, primary: false },
    { label: 'Last 30 days', amount: query.data?.revenueLast30Days, primary: false },
  ];

  return (
    <ul className="grid gap-4 sm:grid-cols-3">
      {cards.map((card) => (
        <li
          key={card.label}
          className={`rounded-panel border p-5 ${card.primary ? 'border-brand bg-brand-tint' : 'border-line bg-surface'}`}
        >
          <p className="text-sm font-medium text-ink-soft">{card.label}</p>
          {card.amount === undefined ? (
            <Skeleton className="mt-2 h-9 w-40" />
          ) : (
            <p className="mt-1 text-3xl font-bold tabular-nums tracking-tight">{formatPeso(card.amount)}</p>
          )}
        </li>
      ))}
    </ul>
  );
}
