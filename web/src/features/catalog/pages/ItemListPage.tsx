import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { useCategories, useItems } from '../queries';
import { StaleDataNotice } from '../../../components/feedback/StaleDataNotice';
import { SearchBar } from '../../../components/SearchBar';
import { EmptyState } from '../../../components/EmptyState';
import { ErrorState, describeQueryError } from '../../../components/ErrorState';
import { SkeletonRows } from '../../../components/Skeleton';
import { StatusBadge } from '../../../components/StatusBadge';
import { useTenantSettings } from '../../tenant/queries';
import { PricingType } from '../types';
import { pricingTypeLabels } from '../labels';

export function ItemListPage() {
  const { data: items, isLoading, isError, error, refetch, dataUpdatedAt } = useItems();
  const { data: categories } = useCategories();
  // Unknown (a non-Admin, or still loading) reads as off, exactly like the Flutter client.
  const showRecipe = useTenantSettings().data?.useSeparateInventoryTracking === true;
  const [search, setSearch] = useState('');

  const categoryNameById = useMemo(() => {
    const map = new Map<string, string>();
    for (const c of categories ?? []) map.set(c.id, c.name);
    return map;
  }, [categories]);

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
        <div className="overflow-x-auto rounded-lg border border-gray-200 bg-white">
          <table className="min-w-full divide-y divide-gray-200 text-sm">
            <thead className="bg-gray-50 text-left text-xs font-medium uppercase text-gray-500">
              <tr>
                <th className="px-4 py-2">Item</th>
                <th className="px-4 py-2">Category</th>
                <th className="px-4 py-2">Pricing</th>
                <th className="px-4 py-2">Price</th>
                <th className="px-4 py-2">Stock</th>
                <th className="px-4 py-2" />
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {filteredItems.map((item) => (
                <tr key={item.id}>
                  <td className="flex items-center gap-2 px-4 py-2">
                    {item.imageUrl ? (
                      <img src={item.imageUrl} alt="" className="h-8 w-8 rounded object-cover" />
                    ) : (
                      <div className="h-8 w-8 rounded bg-gray-100" />
                    )}
                    <div>
                      <p className="font-medium text-gray-900">{item.name}</p>
                      {item.sku && <p className="text-xs text-gray-500">SKU {item.sku}</p>}
                    </div>
                  </td>
                  <td className="px-4 py-2 text-gray-600">
                    {item.categoryId ? categoryNameById.get(item.categoryId) ?? '—' : 'Uncategorized'}
                  </td>
                  <td className="px-4 py-2 text-gray-600">{pricingTypeLabels[item.pricingType]}</td>
                  <td className="px-4 py-2 text-gray-900">₱{item.basePrice.toFixed(2)}</td>
                  <td className="px-4 py-2">
                    {item.isOutOfStock ? (
                      <StatusBadge label="Out of stock" tone="danger" />
                    ) : (
                      <span className="text-gray-600">{item.stockOnHand}</span>
                    )}
                    {!item.isActive && <StatusBadge label="Inactive" tone="neutral" />}
                  </td>
                  <td className="px-4 py-2 text-right">
                    <ItemRowActions itemId={item.id} pricingType={item.pricingType} showRecipe={showRecipe} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
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
