import { useState } from 'react';
import { CategoryStrip } from './components/CategoryStrip';
import { useNavigate } from 'react-router-dom';
import { ErrorState } from '../../components/ErrorState';
import { Skeleton } from '../../components/Skeleton';
import { LinkButton } from '../../components/PageHeader';
import { toast } from '../../components/feedback/toastStore';
import { userMessage } from '../../lib/apiError';
import { catalogApi } from '../catalog/api';
import { catalogKeys, useCategories, useItems, useModifierGroups } from '../catalog/queries';
import type { Item } from '../catalog/types';
import { useQueryClient } from '@tanstack/react-query';
import { useSession } from '../auth/useSession';
import { formatPeso } from '../dashboard/format';
import { useOnlineStatus } from '../../hooks/useOnlineStatus';
import { addFlowFor, filterItems, findByCode } from './catalogView';
import { CartPanel } from './components/CartPanel';
import { DeviceRequired } from './components/DeviceRequired';
import { ItemGrid } from './components/ItemGrid';
import { OptionsDialog } from './components/OptionsDialog';
import { WeightDialog } from './components/WeightDialog';
import { useCart, usePosAdds } from './queries';
import { CUSTOMER_DISPLAY_PATH, customerDisplaySupported, stateForCart } from '../../hardware/display/channel';
import { usePublishCustomerDisplay } from '../../hardware/display/usePublishCustomerDisplay';
import { CameraScanDialog } from '../../hardware/scanner/CameraScanDialog';
import { cameraScanSupported } from '../../hardware/scanner/cameraSupport';
import { useBarcodeWedge } from '../../hardware/scanner/useBarcodeWedge';
import type { PendingRow } from './addQueue';
import type { AddLineRequest } from './types';

type Dialog = { kind: 'options'; item: Item } | { kind: 'weight'; item: Item } | null;

export function SellPage() {
  const { claims, role } = useSession();
  if (!claims?.deviceId) return <DeviceRequired />;
  return <Register isSupervisor={role === 'Admin' || role === 'Manager'} />;
}

function Register({ isSupervisor }: { isSupervisor: boolean }) {
  const navigate = useNavigate();
  const online = useOnlineStatus();
  const items = useItems();
  const categories = useCategories();
  const cart = useCart(online);
  const adds = usePosAdds();
  const modifierGroups = useModifierGroups();
  const qc = useQueryClient();
  const [categoryId, setCategoryId] = useState<string | null>(null);
  const [query, setQuery] = useState('');
  const [dialog, setDialog] = useState<Dialog>(null);
  const [cartOpen, setCartOpen] = useState(false);
  const [cameraOpen, setCameraOpen] = useState(false);

  const visible = filterItems(items.data ?? [], { categoryId, query });
  const pendingCount = adds.pending.reduce((sum, row) => sum + (Number.isInteger(row.quantity) ? row.quantity : 1), 0);
  const lineCount = (cart.data?.lines.reduce((sum, line) => sum + (Number.isInteger(line.quantity) ? line.quantity : 1), 0) ?? 0) + pendingCount;

  /** Records the add and closes any dialog at once; the cart catches up as the server answers. */
  function add(label: string, request: AddLineRequest) {
    setDialog(null);
    adds.add(label, request);
  }

  // A business with no modifier groups at all cannot have any attached to an item, so the per-item check is skipped.
  const mayHaveModifiers = modifierGroups.data === undefined || modifierGroups.data.length > 0;

  async function beginAdd(item: Item) {
    const flow = addFlowFor(item);
    if (flow === 'weight') return setDialog({ kind: 'weight', item });
    if (flow === 'variant' || flow === 'combo') return setDialog({ kind: 'options', item });

    // A plain item goes straight in, with no waiting, unless it has modifier groups to choose from.
    const plainAdd = () => add(item.name, { itemId: item.id, itemVariantId: null, quantity: 1 });
    if (!mayHaveModifiers) return plainAdd();
    try {
      const groups = await qc.fetchQuery({ queryKey: catalogKeys.itemModifierGroups(item.id), queryFn: () => catalogApi.listItemModifierGroups(item.id), staleTime: 5 * 60_000 });
      if (groups.length > 0) setDialog({ kind: 'options', item });
      else plainAdd();
    } catch (error) {
      toast.error(userMessage(error));
    }
  }

  function scanCode(code: string) {
    const match = findByCode(items.data ?? [], code);
    if (!match) {
      toast.info('No item with that barcode or SKU');
      return;
    }
    void beginAdd(match);
  }

  usePublishCustomerDisplay(stateForCart(cart.data));
  useBarcodeWedge(scanCode, online && dialog === null && !cameraOpen);

  function onSearchKey(event: React.KeyboardEvent<HTMLInputElement>) {
    if (event.key !== 'Enter') return;
    event.preventDefault();
    const code = query.trim();
    if (!code) return;
    const match = findByCode(items.data ?? [], code);
    if (!match) {
      toast.info('No item with that barcode or SKU');
      return;
    }
    setQuery('');
    void beginAdd(match);
  }

  if (!online) {
    return <ErrorState title="Selling needs a connection" message="Prices and stock are worked out by the server, so the till is paused while you are offline. It resumes when the connection returns." />;
  }

  return (
    <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_24rem]">
      <div className="flex min-w-0 flex-col gap-4">
        <div className="flex flex-wrap gap-2">
          <LinkButton to="/sell/kiosk-orders">Kiosk orders</LinkButton>
          <LinkButton to="/sell/shift">Shift and drawer</LinkButton>
          <LinkButton to="/sell/hardware">Hardware</LinkButton>
          {cameraScanSupported() && (
            <button type="button" onClick={() => setCameraOpen(true)} className="inline-flex h-12 items-center rounded-control border border-line bg-surface px-4 text-base font-semibold hover:border-brand">
              Scan with camera
            </button>
          )}
          {customerDisplaySupported() && (
            <button type="button" onClick={() => window.open(CUSTOMER_DISPLAY_PATH, 'purch-customer-display')} className="inline-flex h-12 items-center rounded-control border border-line bg-surface px-4 text-base font-semibold hover:border-brand">
              Customer display
            </button>
          )}
        </div>
        <input
          type="search"
          aria-label="Search items or scan a barcode"
          placeholder="Search or scan a barcode"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          onKeyDown={onSearchKey}
          className="h-14 w-full rounded-control border border-ink-soft/40 bg-surface px-4 text-lg"
        />

        <CategoryStrip categories={categories.data ?? []} selectedId={categoryId} onSelect={setCategoryId} />

        {items.isPending && (
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 xl:grid-cols-4" aria-busy="true">
            {Array.from({ length: 8 }, (_, i) => (
              <Skeleton key={i} className="h-52 w-full" />
            ))}
          </div>
        )}
        {items.isError && <ErrorState title="Items could not be loaded" message={userMessage(items.error)} onRetry={() => void items.refetch()} />}
        {items.isSuccess && <ItemGrid items={visible} onPick={(item) => void beginAdd(item)} />}
      </div>

      <aside className="hidden lg:sticky lg:top-4 lg:block lg:h-[calc(100dvh-6rem)]">
        <CartArea cart={cart} isSupervisor={isSupervisor} pending={adds.pending} onCheckout={() => navigate('/sell/payment')} />
      </aside>

      <div className="fixed inset-x-4 bottom-24 z-30 lg:hidden">
        <button
          type="button"
          onClick={() => setCartOpen(true)}
          className="flex h-16 w-full items-center justify-between rounded-control bg-brand px-5 text-lg font-bold text-on-brand shadow-lg"
        >
          <span>Review cart ({lineCount})</span>
          <span className="tabular-nums">{cart.data ? formatPeso(cart.data.totalAmount) : ''}</span>
        </button>
      </div>

      {cartOpen && (
        <div className="fixed inset-0 z-50 flex flex-col gap-3 bg-canvas p-4 lg:hidden">
          <button type="button" onClick={() => setCartOpen(false)} className="h-12 self-start text-base font-semibold text-brand-strong underline">
            Back to items
          </button>
          <div className="min-h-0 flex-1">
            <CartArea cart={cart} isSupervisor={isSupervisor} pending={adds.pending} onCheckout={() => navigate('/sell/payment')} />
          </div>
        </div>
      )}

      {cameraOpen && (
        <CameraScanDialog
          onDetect={(code) => {
            setCameraOpen(false);
            scanCode(code);
          }}
          onClose={() => setCameraOpen(false)}
        />
      )}
      {dialog?.kind === 'options' && <OptionsDialog item={dialog.item} items={items.data ?? []} busy={false} onAdd={(request) => add(dialog.item.name, request)} onClose={() => setDialog(null)} />}
      {dialog?.kind === 'weight' && (
        <WeightDialog item={dialog.item} busy={false} onAdd={(quantity) => add(dialog.item.name, { itemId: dialog.item.id, itemVariantId: null, quantity })} onClose={() => setDialog(null)} />
      )}
    </div>
  );
}

function CartArea({ cart, isSupervisor, pending, onCheckout }: { cart: ReturnType<typeof useCart>; isSupervisor: boolean; pending: PendingRow[]; onCheckout: () => void }) {
  if (cart.isPending) return <Skeleton className="h-full min-h-96 w-full" />;
  if (cart.isError) return <ErrorState title="The cart could not be loaded" message={userMessage(cart.error)} onRetry={() => void cart.refetch()} />;
  return <CartPanel cart={cart.data} isSupervisor={isSupervisor} pending={pending} onCheckout={onCheckout} />;
}
