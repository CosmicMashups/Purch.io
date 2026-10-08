import { useMemo, useState } from 'react';
import { PurchImage } from '../../../components/brand/PurchImage';
import { useNavigate } from 'react-router-dom';
import { ItemDialog } from './ItemDialog';
import { StockBranchPicker, StockStepper, useStockBranch } from '../../../components/StockStepper';
import { useRecordMovement } from '../../inventory/queries';
import { MovementType } from '../../inventory/types';
import { useDepartmentTracking } from '../../tenant/queries';
import { useCategories, useItems, useReorderItems } from '../queries';
import { SortableGroupedTable, type SortableColumn, type SortableGroup } from '../../../components/lists/SortableGroupedTable';
import { toast } from '../../../components/feedback/toastStore';
import { StaleDataNotice } from '../../../components/feedback/StaleDataNotice';
import { SearchBar } from '../../../components/SearchBar';
import { EmptyState } from '../../../components/EmptyState';
import { ErrorState, describeQueryError } from '../../../components/ErrorState';
import { SkeletonRows } from '../../../components/Skeleton';
import { StatusBadge } from '../../../components/StatusBadge';
import { PricingType, type Item } from '../types';
import { pricingTypeLabels } from '../labels';
import { PageHeader } from '../../../components/PageHeader';
import { RowActionsMenu, type RowAction } from '../../../components/RowActionsMenu';
import { DeletedRecordsPanel, StatusFilter, type StatusView } from '../../lifecycle/StatusFilter';
import { useLifecycle } from '../../lifecycle/useLifecycle';

export function ItemListPage() {
  const { data: items, isLoading, isError, error, refetch, dataUpdatedAt } = useItems();
  const { data: categories } = useCategories();
  const [search, setSearch] = useState('');
  const [view, setView] = useState<StatusView>('active');
  const { run, dialog: lifecycleDialog } = useLifecycle();
  const departmentsOn = useDepartmentTracking();
  // null: closed; 'new': adding; otherwise the item being edited (looked up live so its stock stays current).
  const [dialog, setDialog] = useState<'new' | string | null>(null);
  const editing = dialog && dialog !== 'new' ? (items ?? []).find((i) => i.id === dialog) : undefined;
  const stockBranch = useStockBranch();
  const recordMovement = useRecordMovement();

  async function setStock(item: Item, next: number) {
    if (!stockBranch.branchId) {
      toast.error('No branch is available to record this stock change against.');
      return;
    }
    await recordMovement.mutateAsync({
      itemId: item.id,
      branchId: stockBranch.branchId,
      type: MovementType.Adjustment,
      quantity: next - (item.countOnHand ?? item.stockOnHand),
      note: 'Quick stock change from the Items page',
      reasonCategory: null,
      photoUrl: null,
      supplierReference: null,
    });
  }

  const reorder = useReorderItems();
  const searching = search.trim() !== '';

  const filteredItems = useMemo(() => {
    const q = search.trim().toLowerCase();
    const inView = (items ?? []).filter((item) => (view === 'active' ? item.isActive : !item.isActive));
    if (!q) return inView;
    return inView.filter(
      (item) =>
        item.name.toLowerCase().includes(q) ||
        item.sku?.toLowerCase().includes(q) ||
        item.barcode?.toLowerCase().includes(q),
    );
  }, [items, search, view]);

  // One group per category in the shop's own order, then the items that have none. Within a group, the saved order.
  const groups = useMemo<SortableGroup<Item>[]>(() => {
    const byOrder = (a: Item, b: Item) => a.sortOrder - b.sortOrder || a.name.localeCompare(b.name);
    const known = new Set((categories ?? []).map((c) => c.id));
    const result: SortableGroup<Item>[] = [...(categories ?? [])]
      .sort((a, b) => a.sortOrder - b.sortOrder)
      .map((c) => ({ id: c.id, title: c.name, rows: filteredItems.filter((i) => i.categoryId === c.id).sort(byOrder) }));
    const uncategorised = filteredItems.filter((i) => i.categoryId === null || !known.has(i.categoryId)).sort(byOrder);
    result.push({ id: 'uncategorized', title: 'Uncategorized', rows: uncategorised });
    return result.filter((g) => g.rows.length > 0);
  }, [filteredItems, categories]);

  const columns: SortableColumn<Item>[] = [
    {
      header: 'Item',
      cell: (item) => (
        <div className="flex items-center gap-2">
          {item.imageUrl ? (
            <PurchImage src={item.imageUrl} alt="" className="h-8 w-8 rounded object-cover" errorNode={<div className="h-8 w-8 rounded bg-gray-100" />} />
          ) : (
            <div className="h-8 w-8 rounded bg-gray-100" />
          )}
          <div>
            <p className="font-medium text-gray-900">{item.name}</p>
            {item.sku && <p className="text-xs text-gray-500">SKU {item.sku}</p>}
          </div>
        </div>
      ),
    },
    { header: 'Pricing', className: 'text-gray-600', cell: (item) => pricingTypeLabels[item.pricingType] },
    { header: 'Price', className: 'text-gray-900', cell: (item) => `₱${item.basePrice.toFixed(2)}` },
    {
      header: 'Stock',
      cell: (item) => (
        <div className="flex flex-col items-start gap-1">
          {item.hasOwnStock === false ? (
            <span className="text-xs text-gray-500">{item.pricingType === PricingType.Service || item.pricingType === PricingType.Combo ? 'No stock' : 'From ingredients'}</span>
          ) : (
            <StockStepper value={item.countOnHand ?? item.stockOnHand} label={item.name} disabled={recordMovement.isPending || !stockBranch.branchId} onSet={(next) => setStock(item, next)} />
          )}
          {item.isOutOfStock && <StatusBadge label="Out of stock" tone="danger" />}
          {!item.isActive && <StatusBadge label="Inactive" tone="neutral" />}
        </div>
      ),
    },
    {
      header: '',
      className: 'text-right',
      cell: (item) => <ItemRowActions item={item} showDepartment={departmentsOn} onEdit={() => setDialog(item.id)} onLifecycle={(action) => run({ kind: 'Item', id: item.id, name: item.name }, action)} />,
    },
  ];

  return (
    <div className="flex flex-col gap-4">
      <PageHeader
        title="Items"
        backTo={{ to: '/business', label: 'Business' }}
        action={
          <button type="button" onClick={() => setDialog('new')} className="h-12 whitespace-nowrap rounded-control bg-brand px-6 text-base font-semibold text-on-brand hover:bg-brand-strong">
            Add item
          </button>
        }
      />
      <div className="flex flex-wrap items-center gap-3">
        <StatusFilter value={view} onChange={setView} />
        {view !== 'deleted' && <SearchBar value={search} onChange={setSearch} placeholder="Search items…" />}
      </div>
      {view === 'deleted' && <DeletedRecordsPanel kind="Item" noun="items" />}

      <StockBranchPicker branches={stockBranch.branches} branchId={stockBranch.branchId} onChange={stockBranch.setBranchId} />

      <StaleDataNotice updatedAt={dataUpdatedAt} what="items" />

      {view !== 'deleted' && isLoading && <SkeletonRows columns={5} />}

      {view !== 'deleted' && isError && <ErrorState message={describeQueryError(error)} onRetry={() => refetch()} />}

      {view !== 'deleted' && !isLoading && !isError && filteredItems.length === 0 && (
        <EmptyState
          title="No items found"
          description={search ? 'Try a different search.' : 'Add your first item to get started.'}
        />
      )}

      {view !== 'deleted' && !isError && filteredItems.length > 0 && (
        <>
          {searching && <p className="text-xs text-gray-500">Clear the search to change the order of items.</p>}
          <SortableGroupedTable
            groups={groups}
            columns={columns}
            getId={(item) => item.id}
            rowLabel={(item) => item.name}
            disabled={searching || reorder.isPending}
            onReorder={(ids) => reorder.mutate(ids, { onError: () => toast.error('Could not save the new order') })}
          />
        </>
      )}
      {dialog === 'new' && <ItemDialog item={null} onClose={() => setDialog(null)} />}
      {editing && <ItemDialog item={editing} onClose={() => setDialog(null)} />}
      {lifecycleDialog}
    </div>
  );
}

function ItemRowActions({
  item,
  showDepartment,
  onEdit,
  onLifecycle,
}: {
  item: Item;
  showDepartment: boolean;
  onEdit: () => void;
  onLifecycle: (action: 'deactivate' | 'reactivate' | 'delete') => void;
}) {
  const navigate = useNavigate();
  const itemId = item.id;
  const go = (path: string) => () => navigate(`/catalog/items/${itemId}/${path}`);
  const pricingType = item.pricingType;
  const actions: RowAction[] = [{ label: 'Edit', onSelect: onEdit }];

  if (pricingType === PricingType.WeightVolume) {
    actions.push({ label: 'Batches', onSelect: go('batches') }, { label: 'Tingi settings', onSelect: go('tingi-config') });
  }
  if (pricingType === PricingType.Bundle) actions.push({ label: 'Bundle rules', onSelect: go('bundle-rules') });
  if (pricingType === PricingType.VariantMatrix) actions.push({ label: 'Variants', onSelect: go('variants') });
  if (pricingType === PricingType.Service) actions.push({ label: 'Service duration', onSelect: go('service-duration') });
  if (pricingType === PricingType.Combo) actions.push({ label: 'Combo parts', onSelect: go('combo-components') });
  actions.push({ label: 'Modifier groups', onSelect: go('modifier-groups') });
  if (showDepartment) actions.push({ label: 'Department', onSelect: go('department') });

  actions.push(
    item.isActive
      ? { label: 'Make inactive', onSelect: () => onLifecycle('deactivate'), separated: true }
      : { label: 'Make active', onSelect: () => onLifecycle('reactivate'), separated: true },
    { label: 'Delete', danger: true, onSelect: () => onLifecycle('delete') },
  );

  return (
    <div className="flex justify-end">
      <RowActionsMenu subject={item.name} actions={actions} />
    </div>
  );
}
