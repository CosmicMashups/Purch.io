import { useEffect, useState } from 'react';
import { BrandMark, Wordmark } from '../../components/brand/Brand';
import { PurchImage } from '../../components/brand/PurchImage';
import { formatPeso } from '../../features/dashboard/format';
import { BUNDLED } from '../../lib/images';
import { readBrandCache } from '../../theme/brandCache';
import { IDLE_STATE, customerDisplaySupported, subscribeCustomerDisplay, type CustomerDisplayState } from './channel';

/**
 * A second window for the customer, dragged to the customer-facing monitor. It shows only what the till
 * sends, on the same browser, and never talks to the server or shows staff details.
 *
 * Upright screen: top bar, poster, then the order. Wide screen: top bar, then the poster on the left and
 * the order on the right.
 */
export function CustomerDisplayPage() {
  const [state, setState] = useState<CustomerDisplayState>(IDLE_STATE);
  const [{ businessName, kioskPosterImageUrl }] = useState(() => readBrandCache() ?? {});
  const supported = customerDisplaySupported();

  useEffect(() => subscribeCustomerDisplay(setState), []);

  if (!supported) {
    return (
      <main className="grid min-h-dvh place-items-center bg-canvas p-8 text-center text-ink">
        <p className="max-w-md text-xl">This browser cannot share the order between windows. Use a current version of Chrome, Edge or Firefox.</p>
      </main>
    );
  }

  const idle = state.mode === 'idle';

  return (
    <main className="flex h-dvh flex-col overflow-hidden bg-canvas text-ink">
      <header className="flex shrink-0 items-center justify-between gap-6 bg-brand-strong px-8 py-4 text-on-brand">
        <div className="flex min-w-0 items-center gap-4">
          <BrandMark size={56} />
          {businessName && <p className="truncate text-2xl font-bold tracking-tight">{businessName}</p>}
        </div>
        <Wordmark height={26} onBrand />
      </header>

      <div className="flex min-h-0 flex-1 flex-col landscape:grid landscape:grid-cols-[minmax(0,5fr)_minmax(0,6fr)]">
        <div className="h-[22dvh] shrink-0 p-4 landscape:h-auto landscape:p-6">
          <PurchImage
            src={kioskPosterImageUrl ?? null}
            fallback={BUNDLED.kioskPoster}
            alt=""
            loading="eager"
            className="size-full rounded-panel object-cover"
            errorNode={<span aria-hidden="true" />}
          />
        </div>

        {idle ? (
          <section className="grid min-h-0 flex-1 place-items-center bg-brand p-8 text-on-brand">
            <h1 className="motion-safe:animate-[fade-up_700ms_cubic-bezier(0.16,1,0.3,1)] text-[clamp(3.5rem,9vw,9rem)] font-extrabold leading-none tracking-tighter">Welcome!</h1>
          </section>
        ) : (
          <Order state={state} />
        )}
      </div>
    </main>
  );
}

function heading(mode: CustomerDisplayState['mode']): string {
  if (mode === 'payment') return 'Amount to pay';
  return mode === 'completed' ? 'Thank you!' : 'Your order';
}

function Order({ state }: { state: CustomerDisplayState }) {
  const discount = state.savings.reduce((sum, s) => sum + s.amount, 0);

  return (
    <section className="flex min-h-0 flex-1 flex-col gap-2 p-4 pb-5 landscape:p-6 landscape:pb-6 landscape:pl-0" aria-label="Order">
      <h1 className="text-2xl font-bold tracking-tight">{heading(state.mode)}</h1>

      <div className="min-h-0 flex-1 overflow-y-auto">
        <table className="w-full text-left text-lg">
          <thead className="sticky top-0 bg-canvas text-sm font-semibold text-ink-soft">
            <tr className="border-b border-line">
              <th scope="col" className="py-1 pr-3 font-semibold">Item</th>
              <th scope="col" className="px-3 py-1 text-right font-semibold">Quantity</th>
              <th scope="col" className="px-3 py-1 text-right font-semibold">Price</th>
              <th scope="col" className="py-1 pl-3 text-right font-semibold">Total</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-line">
            {state.lines.map((line, i) => (
              <tr key={i}>
                <td className="py-1.5 pr-3 font-medium">{line.name}</td>
                <td className="px-3 py-1.5 text-right tabular-nums">{line.quantity}</td>
                <td className="px-3 py-1.5 text-right tabular-nums text-ink-soft">{formatPeso(line.unitPrice)}</td>
                <td className="py-1.5 pl-3 text-right font-semibold tabular-nums">{formatPeso(line.lineTotal)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <dl className="grid shrink-0 grid-cols-[1fr_auto] items-baseline gap-x-6 gap-y-1.5 border-t-2 border-ink pt-3 text-xl">
        <Figure label="Sub-Total" value={state.subtotal} />
        <Figure label="Discount" note={state.savings.length === 1 ? state.savings[0].label : undefined} value={-discount} muted={discount === 0} />
        {state.savings.length > 1 &&
          state.savings.map((s) => <Figure key={s.label} label={s.label} value={-s.amount} sub />)}
        <Figure label="VAT (12%)" value={state.vat} />
        <div className="col-span-2 mt-1 grid grid-cols-subgrid items-center rounded-control bg-brand px-4 py-3 text-on-brand">
          <dt className="text-2xl font-bold">Total</dt>
          <dd className="text-[clamp(2rem,3.4vw,3.25rem)] font-extrabold leading-none tracking-tight tabular-nums">{formatPeso(state.total)}</dd>
        </div>
        {state.tendered !== null && <Figure label="Paid" value={state.tendered} />}
        {state.change !== null && <Figure label="Change" value={state.change} strong />}
      </dl>
    </section>
  );
}

function Figure({ label, note, value, strong = false, muted = false, sub = false }: { label: string; note?: string; value: number; strong?: boolean; muted?: boolean; sub?: boolean }) {
  const tone = muted ? 'text-ink-soft' : '';
  const size = strong ? 'text-3xl font-extrabold' : sub ? 'pl-4 text-base text-ink-soft' : 'font-medium';
  return (
    <>
      <dt className={`${size} ${tone}`}>
        {label}
        {note && <span className="ml-3 text-base font-normal text-ink-soft">{note}</span>}
      </dt>
      <dd className={`text-right tabular-nums ${size} ${tone}`}>{value < 0 ? `−${formatPeso(-value)}` : formatPeso(value)}</dd>
    </>
  );
}
