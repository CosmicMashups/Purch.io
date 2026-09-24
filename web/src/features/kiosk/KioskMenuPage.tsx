import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useQueryClient } from '@tanstack/react-query';
import { ErrorState } from '../../components/ErrorState';
import { Skeleton } from '../../components/Skeleton';
import { toast } from '../../components/feedback/toastStore';
import { userMessage } from '../../lib/apiError';
import { catalogApi } from '../catalog/api';
import { catalogKeys, useCategories, useItems } from '../catalog/queries';
import type { Item } from '../catalog/types';
import { formatPeso } from '../dashboard/format';
import { addFlowFor, filterItems } from '../pos/catalogView';
import { OptionsDialog } from '../pos/components/OptionsDialog';
import { PricingType } from '../catalog/types';
import type { AddLineRequest } from '../pos/types';
import { useKioskAddLine, useKioskCart } from './queries';

export function KioskMenuPage() {
  const items = useItems();
  const categories = useCategories();
  const cart = useKioskCart();
  const addLine = useKioskAddLine();
  const qc = useQueryClient();
  const [categoryId, setCategoryId] = useState<string | null>(null);
  const [dialog, setDialog] = useState<Item | null>(null);
  const [resolving, setResolving] = useState(false);

  const visible = filterItems(items.data ?? [], { categoryId, query: '' });
  const count = cart.data?.lines.reduce((sum, line) => sum + line.quantity, 0) ?? 0;
  const busy = addLine.isPending || resolving;

  function add(request: AddLineRequest, item?: Item) {
    addLine.mutate(request, {
      onSuccess: () => {
        setDialog(null);
        if (item) toast.success(`${item.name} added to your order`);
      },
    });
  }

  async function pick(item: Item) {
    const flow = addFlowFor(item);
    if (flow === 'weight') {
      toast.info(`${item.name} is not available here yet. Please order at the counter.`);
      return;
    }
    if (flow === 'variant' || flow === 'combo') return setDialog(item);

    setResolving(true);
    try {
      const groups = await qc.fetchQuery({ queryKey: catalogKeys.itemModifierGroups(item.id), queryFn: () => catalogApi.listItemModifierGroups(item.id), staleTime: 60_000 });
      if (groups.length > 0) setDialog(item);
      else add({ itemId: item.id, itemVariantId: null, quantity: 1 }, item);
    } catch (error) {
      toast.error(userMessage(error));
    } finally {
      setResolving(false);
    }
  }

  return (
    <div className="flex flex-1 flex-col">
      <header className="sticky top-0 z-10 flex flex-col gap-3 border-b border-line bg-canvas px-4 py-3">
        <div className="flex items-center justify-between gap-3">
          <Link to="/kiosk" className="inline-flex h-12 items-center text-base font-semibold text-brand-strong underline">
            Start over
          </Link>
          <h1 className="text-xl font-bold">Menu</h1>
          <span className="w-20" aria-hidden="true" />
        </div>
        <div role="group" aria-label="Categories" className="-mx-4 flex gap-2 overflow-x-auto px-4 pb-1">
          <Chip active={categoryId === null} onClick={() => setCategoryId(null)}>
            All
          </Chip>
          {[...(categories.data ?? [])]
            .sort((a, b) => a.sortOrder - b.sortOrder)
            .map((c) => (
              <Chip key={c.id} active={categoryId === c.id} onClick={() => setCategoryId(c.id)}>
                {c.name}
              </Chip>
            ))}
        </div>
      </header>

      <main className="flex-1 p-4 pb-32">
        {items.isPending && (
          <div className="grid grid-cols-2 gap-4" aria-busy="true">
            {Array.from({ length: 6 }, (_, i) => (
              <Skeleton key={i} className="h-44 w-full" />
            ))}
          </div>
        )}
        {items.isError && <ErrorState title="The menu could not be loaded" message={userMessage(items.error)} onRetry={() => void items.refetch()} />}
        {items.isSuccess && visible.length === 0 && <p className="rounded-panel border border-dashed border-ink-soft/40 p-8 text-center text-lg text-ink-soft">Nothing here right now. Try another category.</p>}
        {items.isSuccess && visible.length > 0 && (
          <ul className="grid grid-cols-2 gap-4">
            {visible.map((item) => (
              <li key={item.id}>
                <button
                  type="button"
                  disabled={busy || item.isOutOfStock}
                  onClick={() => void pick(item)}
                  className="flex h-full min-h-44 w-full flex-col overflow-hidden rounded-panel border border-line bg-surface text-left active:translate-y-px disabled:opacity-50"
                >
                  {item.imageUrl && <img src={item.imageUrl} alt="" loading="lazy" className="aspect-[4/3] w-full object-cover" />}
                  <span className="flex flex-1 flex-col justify-between gap-2 p-4">
                    <span className="line-clamp-2 text-lg font-semibold leading-snug">{item.name}</span>
                    <span className="text-lg font-bold tabular-nums text-brand-strong">
                      {item.isOutOfStock ? 'Sold out' : item.pricingType === PricingType.VariantMatrix ? 'Choose option' : formatPeso(item.basePrice)}
                    </span>
                  </span>
                </button>
              </li>
            ))}
          </ul>
        )}
      </main>

      <div className="fixed inset-x-0 bottom-0 z-20 mx-auto max-w-xl bg-gradient-to-t from-canvas via-canvas to-transparent p-4">
        <Link to="/kiosk/cart" className="flex h-16 items-center justify-between rounded-control bg-brand px-6 text-xl font-bold text-on-brand">
          <span>View your order ({count})</span>
          <span className="tabular-nums">{cart.data ? formatPeso(cart.data.totalAmount) : ''}</span>
        </Link>
      </div>

      {dialog && <OptionsDialog item={dialog} items={items.data ?? []} busy={addLine.isPending} onAdd={(request) => add(request, dialog)} onClose={() => setDialog(null)} />}
    </div>
  );
}

function Chip({ active, onClick, children }: { active: boolean; onClick: () => void; children: React.ReactNode }) {
  return (
    <button
      type="button"
      aria-pressed={active}
      onClick={onClick}
      className={`h-14 shrink-0 rounded-full px-6 text-lg font-semibold ${active ? 'bg-brand text-on-brand' : 'border border-line bg-surface'}`}
    >
      {children}
    </button>
  );
}
