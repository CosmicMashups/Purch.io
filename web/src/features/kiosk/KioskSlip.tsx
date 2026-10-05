import type { PaperWidth } from '../../hardware/config';
import { formatPeso } from '../dashboard/format';
import type { SlipData } from './slipData';

/** The slip as it prints on thermal paper. It only ever appears on the printed page; on screen it is hidden by `.kiosk-slip`. */
export function KioskSlip({ slip, paperWidth }: { slip: SlipData; paperWidth: PaperWidth }) {
  return (
    <div className={`kiosk-slip receipt-${paperWidth} font-mono text-sm leading-snug text-black`} aria-hidden="true">
      <p className="text-center text-base font-bold uppercase">{slip.businessName}</p>
      {slip.test ? <p className="mt-1 text-center font-bold">*** TEST PRINT ***</p> : <p className="mt-1 text-center">ORDER SLIP (not an official receipt)</p>}

      <p className="mt-3 text-center">Order number</p>
      <p className="text-center text-5xl font-black leading-none">{slip.orderNumber}</p>
      {slip.orderType && <p className="mt-1 text-center font-bold">{slip.orderType}</p>}
      <p className="mt-1 text-center text-xs">
        {slip.printedAt.toLocaleDateString('en-PH')} {slip.printedAt.toLocaleTimeString('en-PH', { hour: '2-digit', minute: '2-digit' })}
      </p>

      <ul className="mt-3 border-y border-dashed border-black py-2">
        {slip.lines.map((line, index) => (
          <li key={index} className="mb-1">
            <span className="font-bold">{line.quantity} x</span> {line.name}
            {line.details.length > 0 && <span className="block pl-6 text-xs">{line.details.join(', ')}</span>}
          </li>
        ))}
      </ul>

      {!slip.test && (
        <p className="mt-2 flex justify-between font-bold">
          <span>{slip.discounted ? 'Total before discount' : 'Total'}</span>
          <span>{formatPeso(slip.total)}</span>
        </p>
      )}
      {slip.payment && <p className="mt-1">Customer chose: {slip.payment}</p>}
      <p className="mt-3 text-center font-bold">Pay at the counter and enjoy!</p>
    </div>
  );
}
