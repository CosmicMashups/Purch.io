import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Package } from '@phosphor-icons/react';
import { PurchImage } from '../../../components/brand/PurchImage';
import { CompactList, CompactRow } from '../../../components/lists/CompactList';
import { RowActionsMenu, type RowAction } from '../../../components/RowActionsMenu';
import { Tabs } from '../../../components/Tabs';
import { useSession } from '../../auth/useSession';
import { useSelectableBranches } from '../../branches/queries';
import { useItems } from '../../catalog/queries';
import { AsyncPanel } from '../../dashboard/components/AsyncPanel';
import { stockRatio } from '../../dashboard/format';
import { useInventoryDashboard } from '../../dashboard/queries';
import { useRestockSuggestions, useSuppliers } from '../queries';
import { STOCK_SCOPE_TABS, type StockScope } from '../stockScope';
import { MovementType, type RestockSuggestion } from '../types';
import { ItemSuppliersDialog } from './ItemSuppliersDialog';
import { PurchaseRequestDialog } from './PurchaseRequestDialog';

interface Row {
  id: string;
  name: string;
  imageUrl?: string | null;
  onHand: number;
  threshold: number;
  unit: string;
  addStockTo: string;
}

/**
 * What needs stock, worst first, as one compact column. Everything you can do to a row sits in its ⋯ menu; the one
 * action for the whole list (a purchase request) is the filled button in the heading, apart from the Items and
 * Ingredients filter. An ingredient is restocked by receiving a delivery.
 */
export function RestockList({ scope, onScopeChange }: { scope: StockScope; onScopeChange: (scope: StockScope) => void }) {
  const dashboard = useInventoryDashboard(true);
  const items = useItems();
  const navigate = useNavigate();
  const { role } = useSession();
  const canRequest = role === 'Admin' || role === 'Manager' || role === 'Warehouse';
  const suggestions = useRestockSuggestions(canRequest);
  const suppliers = useSuppliers();
  const { branches } = useSelectableBranches();
  const [requesting, setRequesting] = useState<RestockSuggestion[] | null>(null);
  const [linking, setLinking] = useState<RestockSuggestion | null>(null);
  const pictures = new Map((items.data ?? []).map((item) => [item.id, item.imageUrl]));
  const hasIngredients = Boolean(dashboard.data?.ingredients);
  const active: StockScope = hasIngredients ? scope : 'items';

  function rowsFor(d: NonNullable<typeof dashboard.data>): Row[] {
    if (active === 'ingredients') {
      return (d.ingredients?.lowStock ?? []).map((i) => ({
        id: i.inventoryItemId,
        name: i.name,
        onHand: i.quantityOnHand,
        threshold: i.lowStockThreshold,
        unit: ` ${i.baseUnit}`,
        addStockTo: `/inventory/ingredients?receive=${i.inventoryItemId}`,
      }));
    }
    return d.lowStockItems.map((i) => ({
      id: i.itemId,
      name: i.itemName,
      imageUrl: pictures.get(i.itemId),
      onHand: i.stockOnHand,
      threshold: i.lowStockThreshold,
      unit: '',
      addStockTo: `/inventory/movements?record=1&stockRef=item:${i.itemId}&recordType=${MovementType.StockIn}`,
    }));
  }

  const suggestionFor = (id: string) => suggestions.data?.find((s) => s.itemId === id || s.inventoryItemId === id);
  const scopeSuggestions = (suggestions.data ?? []).filter((s) => (active === 'ingredients' ? s.inventoryItemId !== null : s.itemId !== null));

  function actionsFor(row: Row): RowAction[] {
    const suggestion = suggestionFor(row.id);
    const list: RowAction[] = [];
    if (canRequest && suggestion) {
      list.push({ label: 'Request purchase', onSelect: () => setRequesting([suggestion]) });
    }
    list.push({ label: active === 'ingredients' ? 'Receive delivery' : 'Add stock', onSelect: () => navigate(row.addStockTo) });
    if (canRequest && suggestion) {
      list.push({ label: 'Suppliers', onSelect: () => setLinking(suggestion), separated: true });
    }
    return list;
  }

  return (
    <>
      <AsyncPanel
        title="Restock first"
        subtitle="Most urgent first"
        query={dashboard}
        tabs={hasIngredients && <Tabs label="Restock for" tabs={STOCK_SCOPE_TABS} active={active} onChange={onScopeChange} idPrefix="restock" />}
        action={
          canRequest && scopeSuggestions.length > 0 ? (
            <button
              type="button"
              onClick={() => setRequesting(scopeSuggestions)}
              className="h-11 shrink-0 rounded-control bg-brand px-4 text-base font-semibold text-on-brand hover:bg-brand-strong"
            >
              Create purchase request
            </button>
          ) : undefined
        }
        isEmpty={(d) => rowsFor(d).length === 0}
        emptyMessage={active === 'ingredients' ? 'No ingredient is running low right now.' : 'Nothing is running low right now.'}
      >
        {(d) => (
          <div id="restock-panel" role="tabpanel">
            <CompactList label="Needs restocking">
              {rowsFor(d)
                .sort((a, b) => stockRatio(a.onHand, a.threshold) - stockRatio(b.onHand, b.threshold))
                .map((row) => {
                  const out = row.onHand <= 0;
                  return (
                    <CompactRow
                      key={row.id}
                      lead={
                        <span className="grid size-10 shrink-0 place-items-center overflow-hidden rounded-lg bg-canvas text-ink-soft">
                          <PurchImage src={row.imageUrl} alt="" className="size-full object-cover" errorNode={<Package size={20} aria-hidden="true" />} />
                        </span>
                      }
                      title={row.name}
                      detail={`Alert at ${row.threshold}${row.unit}`}
                      aside={
                        <span className={`text-base font-semibold tabular-nums ${out ? 'text-danger' : 'text-warn-strong'}`}>
                          {out ? 'Out of stock' : `${row.onHand}${row.unit} left`}
                        </span>
                      }
                      menu={<RowActionsMenu subject={row.name} actions={actionsFor(row)} />}
                    />
                  );
                })}
            </CompactList>
          </div>
        )}
      </AsyncPanel>
      {requesting && branches && <PurchaseRequestDialog rows={requesting} branches={branches} onClose={() => setRequesting(null)} />}
      {linking && suppliers.data && <ItemSuppliersDialog row={linking} suppliers={suppliers.data} onClose={() => setLinking(null)} />}
    </>
  );
}
