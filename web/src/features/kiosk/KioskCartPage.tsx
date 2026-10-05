import { Minus, PencilSimple, Plus, Trash } from '@phosphor-icons/react';
import { useMemo } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { PurchImage } from '../../components/brand/PurchImage';
import { useItems } from '../catalog/queries';
import type { Item } from '../catalog/types';
import { formatPeso } from '../dashboard/format';
import { useTenantSettings } from '../tenant/queries';
import { barClass, primaryButton, secondaryButton } from './KioskActionBar';
import { toLocalTransaction, useLocalKioskCartStore } from './localCart';
import { useKioskPromoRules } from './queries';
import { quantityCap } from './quantityCap';
import { lineDetails } from './tickets';

const stepper = 'grid size-14 place-items-center rounded-full border-2 border-line bg-surface transition-transform duration-100 hover:border-brand active:scale-95 disabled:opacity-35';

/** Your order: every line with its picture, quantity and choices, each one editable. The last stop before choosing how to pay. */
export function KioskCartPage() {
  const navigate = useNavigate();
  const lines = useLocalKioskCartStore((s) => s.lines);
  const updateQuantity = useLocalKioskCartStore((s) => s.updateQuantity);
  const removeLine = useLocalKioskCartStore((s) => s.removeLine);
  const { data: rules } = useKioskPromoRules();
  const items = useItems();
  const separateTracking = useTenantSettings().data?.useSeparateInventoryTracking;
  const cart = toLocalTransaction(lines, rules);

  const itemsById = useMemo(() => new Map((items.data ?? []).map((item) => [item.id, item] as const)), [items.data]);
  // Only judged once the menu has loaded: an unknown item is not "gone" while it is still on its way.
  const gone = (itemId: string): boolean => items.isSuccess && !isOrderable(itemsById.get(itemId));
  const unavailableCount = cart.lines.filter((line) => gone(line.itemId)).length;

  return (
    <div className="flex h-full flex-col">
      <main className="kiosk-scroll min-h-0 flex-1 overflow-y-auto px-6 pb-6">
        <h1 className="mb-4 mt-2 text-4xl font-extrabold tracking-tight">Your order</h1>

        {cart.lines.length === 0 && (
          <div className="flex flex-col items-center gap-6 py-24 text-center">
            <p className="text-3xl font-bold">Your order is empty</p>
            <Link to="/kiosk/menu" className={primaryButton}>
              Choose items
            </Link>
          </div>
        )}

        {unavailableCount > 0 && (
          <p role="alert" className="mb-4 rounded-panel border border-danger/40 bg-red-50 px-5 py-4 text-xl font-semibold text-danger">
            {unavailableCount === 1 ? 'One item in your order is no longer available.' : `${unavailableCount} items in your order are no longer available.`} Please remove it to continue.
          </p>
        )}

        <ul className="mx-auto flex max-w-5xl flex-col gap-3">
          {cart.lines.map((line) => {
            const item = itemsById.get(line.itemId);
            const details = lineDetails(line);
            const unavailable = gone(line.itemId);
            const orderedElsewhere = lines.filter((other) => other.itemId === line.itemId && other.localId !== line.id).reduce((sum, other) => sum + other.quantity, 0);
            const cap = item ? quantityCap(item, separateTracking, orderedElsewhere) : { max: 99, limitedByStock: false };

            return (
              <li key={line.id} className={`flex gap-5 rounded-panel border bg-surface p-4 ${unavailable ? 'border-danger/50' : 'border-line'}`}>
                <div className="size-28 shrink-0 overflow-hidden rounded-control bg-canvas landscape:size-32">
                  <PurchImage src={item?.imageUrl} alt="" className="size-full object-cover" errorNode={<span className="block size-full bg-brand-tint" />} />
                </div>

                <div className="flex min-w-0 flex-1 flex-col gap-3">
                  <div className="flex items-start justify-between gap-4">
                    <div className="min-w-0">
                      <p className="text-2xl font-bold leading-snug">{line.itemName}</p>
                      {details.length > 0 && (
                        <ul className="text-lg text-ink-soft">
                          {details.map((detail) => (
                            <li key={detail}>{detail}</li>
                          ))}
                        </ul>
                      )}
                      {line.appliedPromoLabel && <p className="text-lg font-semibold text-brand-strong">{line.appliedPromoLabel}</p>}
                      {unavailable && <p className="text-lg font-semibold text-danger">No longer available</p>}
                    </div>
                    <p className="shrink-0 text-2xl font-extrabold tabular-nums">{formatPeso(line.lineTotal)}</p>
                  </div>

                  <div className="mt-auto flex flex-wrap items-center justify-between gap-3">
                    <div className="flex items-center gap-4" role="group" aria-label={`Quantity of ${line.itemName}`}>
                      <button type="button" aria-label={`Fewer ${line.itemName}`} disabled={line.quantity <= 1} onClick={() => updateQuantity(line.id, line.quantity - 1)} className={stepper}>
                        <Minus size={26} weight="bold" aria-hidden="true" />
                      </button>
                      <span className="min-w-10 text-center text-3xl font-extrabold tabular-nums" aria-live="polite">
                        {line.quantity}
                      </span>
                      <button type="button" aria-label={`More ${line.itemName}`} disabled={line.quantity >= cap.max || unavailable} onClick={() => updateQuantity(line.id, line.quantity + 1)} className={stepper}>
                        <Plus size={26} weight="bold" aria-hidden="true" />
                      </button>
                    </div>
                    <div className="flex items-center gap-2">
                      {!unavailable && (
                        <button type="button" onClick={() => navigate(`/kiosk/item/${line.itemId}?line=${line.id}`)} className="inline-flex h-14 items-center gap-2 rounded-control border-2 border-line px-5 text-lg font-bold hover:border-brand">
                          <PencilSimple size={24} weight="bold" aria-hidden="true" />
                          Edit
                        </button>
                      )}
                      <button type="button" onClick={() => removeLine(line.id)} className="inline-flex h-14 items-center gap-2 rounded-control px-4 text-lg font-bold text-danger hover:bg-red-50">
                        <Trash size={24} weight="bold" aria-hidden="true" />
                        Remove
                      </button>
                    </div>
                  </div>
                </div>
              </li>
            );
          })}
        </ul>
      </main>

      {cart.lines.length > 0 && (
        <div className={`${barClass} flex-wrap`}>
          <div className="flex min-w-40 flex-1 flex-col justify-center">
            <span className="text-base font-semibold text-ink-soft">Total</span>
            <span className="text-3xl font-extrabold tabular-nums">{formatPeso(cart.totalAmount)}</span>
          </div>
          <Link to="/kiosk/menu" className={secondaryButton}>
            Back to menu
          </Link>
          {unavailableCount > 0 ? (
            <button type="button" className={primaryButton} disabled>
              Proceed to payment
            </button>
          ) : (
            <Link to="/kiosk/order-type" className={primaryButton}>
              Proceed to payment
            </Link>
          )}
        </div>
      )}
    </div>
  );
}

function isOrderable(item: Item | undefined): boolean {
  return item !== undefined && item.isActive && !item.isOutOfStock;
}
