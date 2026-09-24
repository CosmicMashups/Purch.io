import { Link } from 'react-router-dom';
import { BulletList } from '../../../components/charts/RankedCharts';
import { AsyncPanel } from '../../dashboard/components/AsyncPanel';
import { formatPeso } from '../../dashboard/format';
import { useCreditLedgers } from '../../credit/queries';

const SHOWN = 6;

/** Customer credit: how much is owed in all, and who is closest to their limit. Figures are the server's. */
export function MoneyOwed() {
  const ledgers = useCreditLedgers(true);
  return (
    <AsyncPanel
      title="Money owed"
      subtitle="Utang, and how close each account is to its limit"
      query={ledgers}
      isEmpty={(d) => d.filter((l) => l.isActive && l.balance > 0).length === 0}
      emptyMessage="No customer owes anything right now."
    >
      {(all) => {
        const owing = all.filter((l) => l.isActive && l.balance > 0);
        const total = owing.reduce((sum, l) => sum + l.balance, 0);
        const shown = [...owing].sort((a, b) => b.balance - a.balance).slice(0, SHOWN);
        return (
          <div className="flex flex-col gap-5">
            <div className="flex flex-wrap items-end justify-between gap-x-6 gap-y-1">
              <p className="text-4xl font-bold tracking-tight">{formatPeso(total)}</p>
              <p className="text-sm text-ink-soft">{`owed across ${owing.length} account${owing.length === 1 ? '' : 's'}`}</p>
            </div>
            <BulletList
              mode="ceiling"
              rows={shown.map((l) => ({ key: l.id, label: l.customerFullName, value: l.balance, target: l.creditLimit > 0 ? l.creditLimit : l.balance }))}
              describe={(row) => {
                const limit = shown.find((l) => l.id === row.key)?.creditLimit ?? 0;
                return limit > 0
                  ? { value: formatPeso(row.value), note: `of ${formatPeso(limit)} limit`, alert: row.value > limit }
                  : { value: formatPeso(row.value), note: 'no limit set' };
              }}
            />
            {owing.length > SHOWN && <p className="text-sm text-ink-soft">{`Showing the ${SHOWN} largest balances of ${owing.length}.`}</p>}
            <Link to="/business/customers" className="inline-flex h-12 items-center self-start text-base font-semibold text-brand-strong underline">
              Open customers
            </Link>
          </div>
        );
      }}
    </AsyncPanel>
  );
}
