import { formatDateTime } from '../../../lib/dates';
import { formatPeso } from '../../dashboard/format';
import { BirReadingType, type BirReading } from '../types';

function Row({ label, value, strong = false }: { label: string; value: string; strong?: boolean }) {
  return (
    <div className={`flex items-baseline justify-between gap-4 py-1 ${strong ? 'text-lg font-bold' : 'text-base'}`}>
      <dt className={strong ? '' : 'text-ink-soft'}>{label}</dt>
      <dd className="tabular-nums">{value}</dd>
    </div>
  );
}

function NumberList({ label, numbers }: { label: string; numbers: number[] }) {
  return (
    <div className="py-1">
      <dt className="text-base text-ink-soft">{label}</dt>
      <dd className="text-base tabular-nums">{numbers.length === 0 ? 'None' : numbers.join(', ')}</dd>
    </div>
  );
}

/** The server's reading, laid out as it comes. Nothing is recalculated here. */
export function ReadingCard({ reading }: { reading: BirReading }) {
  const isZ = reading.type === BirReadingType.Z;
  const range =
    reading.beginningReceiptNumber !== null && reading.endingReceiptNumber !== null
      ? `${reading.beginningReceiptNumber} to ${reading.endingReceiptNumber}`
      : 'No receipts';

  return (
    <article aria-label={isZ ? 'Z-reading' : 'X-reading'} className="rounded-panel border border-line bg-surface p-6 print:border-0 print:p-0">
      <header className="border-b border-dashed border-line pb-3">
        <h2 className="text-2xl font-bold tracking-tight">{isZ ? 'Z-reading (end of day)' : 'X-reading (mid-shift)'}</h2>
        <p className="text-sm text-ink-soft">
          Machine {reading.machineIdentificationNumber}, generated {formatDateTime(reading.generatedAt)}
        </p>
      </header>

      <dl className="mt-3 divide-y divide-line">
        <Row label="Receipts" value={range} />
        <Row label="Transactions" value={String(reading.transactionCount)} />
        <Row label="Gross sales" value={formatPeso(reading.grossSales)} />
        <Row label="VATable sales" value={formatPeso(reading.vatableSales)} />
        <Row label="VAT amount" value={formatPeso(reading.vatAmount)} />
        <Row label="Senior / PWD discounts" value={formatPeso(reading.seniorPwdDiscountTotal)} />
        <Row label="Promo discounts" value={formatPeso(reading.promoDiscountTotal)} />
        <Row label="Total discounts" value={formatPeso(reading.totalDiscounts)} />
        <Row label="Net sales" value={formatPeso(reading.netSales)} strong />
        <Row label="Voided" value={`${reading.voidedCount} for ${formatPeso(reading.voidedAmount)}`} />
        <Row label="Grand accumulated (before)" value={formatPeso(reading.oldGrandAccumulatedSales)} />
        <Row label="Grand accumulated (after)" value={formatPeso(reading.newGrandAccumulatedSales)} />
        <Row label="Reset counter" value={String(reading.resetCounter)} />
        <NumberList label="Late receipts" numbers={reading.lateReceiptNumbers} />
        <NumberList label="Missing receipt numbers" numbers={reading.missingReceiptNumbers} />
      </dl>
    </article>
  );
}
