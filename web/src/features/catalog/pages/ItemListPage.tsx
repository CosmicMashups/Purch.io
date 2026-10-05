import { useMemo, useState } from 'react';
import { PurchImage } from '../../../components/brand/PurchImage';
import { Link } from 'react-router-dom';
import { useCategories, useItems, useReorderItems } from '../queries';
import { SortableGroupedTable, type SortableColumn, type SortableGroup } from '../../../components/lists/SortableGroupedTable';
import { toast } from '../../../components/feedback/toastStore';
import { StaleDataNotice } from '../../../components/feedback/StaleDataNotice';
import { SearchBar } from '../../../components/SearchBar';
import { EmptyState } from '../../../components/EmptyState';
import { ErrorState, describeQueryError } from '../../../components/ErrorState';
import { SkeletonRows } from '../../../components/Skeleton';
import { StatusBadge } from '../../../components/StatusBadge';
import { useTenantSettings } from '../../tenant/queries';
import { PricingType, type Item } from '../types';
import { pricingTypeLabels } from '../labels';

export function ItemListPage() {
  const { data: items, isLoading, isError, error, refetch, dataUpdatedAt } = useItems();
  const { data: categories } = useCategories();
  // Unknown (a non-Admin, or still loading) reads as off, exactly like the Flutter client.
  const showRecipe = useTenantSettings().data?.useSeparateInventoryTracking === true;
  const [search, setSearch] = useState('');

  const reorder = useReorderItems();
  const searching = search.trim() !== '';

  const filteredItems = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return items ?? [];
    return (items ?? []).filter(
      (item) =>
        item.name.toLowerCase().includes(q) ||
        item.sku?.toLowerCase().includes(q) ||
        item.barcode?.toLowerCase().includes(q),
    );
  }, [items, search]);

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
        <>
          {item.isOutOfStock ? <StatusBadge label="Out of stock" tone="danger" /> : <span className="text-gray-600">{item.stockOnHand}</span>}
          {!item.isActive && <StatusBadge label="Inactive" tone="neutral" />}
        </>
      ),
    },
    {
      header: '',
      className: 'text-right',
      cell: (item) => <ItemRowActions itemId={item.id} pricingType={item.pricingType} showRecipe={showRecipe} />,
    },
  ];

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between gap-4">
        <SearchBar value={search} onChange={setSearch} placeholder="Search items…" />
        <Link
          to="/catalog/items/new"
          className="whitespace-nowrap rounded-md bg-gray-900 px-3 py-2 text-sm font-medium text-white"
        >
          Add Item
        </Link>
      </div>

      <StaleDataNotice updatedAt={dataUpdatedAt} what="items" />

      {isLoading && <SkeletonRows columns={5} />}

      {isError && <ErrorState message={describeQueryError(error)} onRetry={() => refetch()} />}

      {!isLoading && !isError && filteredItems.length === 0 && (
        <EmptyState
          title="No items found"
          description={search ? 'Try a different search.' : 'Add your first item to get started.'}
        />
      )}

      {!isError && filteredItems.length > 0 && (
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
    </div>
  );
}

function ItemRowActions({ itemId, pricingType, showRecipe }: { itemId: string; pricingType: PricingType; showRecipe: boolean }) {
  const links: { to: string; label: string }[] = [{ to: `/catalog/items/${itemId}/edit`, label: 'Edit' }];

  if (pricingType === PricingType.WeightVolume) {
    links.push(
      { to: `/catalog/items/${itemId}/batches`, label: 'Batches' },
      { to: `/catalog/items/${itemId}/tingi-config`, label: 'Tingi Config' },
    );
  }
  if (pricingType === PricingType.Bundle) {
    links.push({ to: `/catalog/items/${itemId}/bundle-rules`, label: 'Bundle Rules' });
  }
  if (pricingType === PricingType.VariantMatrix) {
    links.push({ to: `/catalog/items/${itemId}/variants`, label: 'Variants' });
  }
  if (pricingType === PricingType.Service) {
    links.push({ to: `/catalog/items/${itemId}/service-duration`, label: 'Service Duration' });
  }
  if (pricingType === PricingType.Combo) {
    links.push({ to: `/catalog/items/${itemId}/combo-components`, label: 'Combo Components' });
  }

  links.push(
    { to: `/catalog/items/${itemId}/modifier-groups`, label: 'Modifier Groups' },
    { to: `/catalog/items/${itemId}/department`, label: 'Assign Department' },
    { to: `/catalog/items/${itemId}/low-stock-threshold`, label: 'Low-Stock Threshold' },
  );
  if (showRecipe) links.push({ to: `/catalog/items/${itemId}/recipe`, label: 'Recipe' });

  return (
    <div className="flex flex-wrap justify-end gap-x-2 gap-y-1 text-xs">
      {links.map((link) => (
        <Link key={link.to} to={link.to} className="text-gray-500 hover:text-gray-900 hover:underline">
          {link.label}
        </Link>
      ))}
    </div>
  );
}
