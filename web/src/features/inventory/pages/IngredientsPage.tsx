import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { toast } from '../../../components/feedback/toastStore';
import { EditorCard } from '../../../components/forms/EditorCard';
import { FormField, controlClass } from '../../../components/forms/FormField';
import { Pill } from '../../../components/lists/QueryList';
import { SortableGroupedTable, type SortableColumn, type SortableGroup } from '../../../components/lists/SortableGroupedTable';
import { ErrorState } from '../../../components/ErrorState';
import { SkeletonList } from '../../../components/Skeleton';
import { userMessage } from '../../../lib/apiError';
import { PageHeader } from '../../../components/PageHeader';
import { Tabs } from '../../../components/Tabs';
import { useSession } from '../../auth/useSession';
import { IngredientCategories } from '../components/IngredientCategories';
import type { Branch } from '../../branches/types';
import { useSelectableBranches } from '../../branches/queries';
import { countSchema, ingredientSchema, parseThreshold, receiveSchema, type CountForm, type IngredientForm, type ReceiveForm } from '../ingredient';
import {
  useCreateInventoryItem,
  useInventoryCategories,
  useInventoryItems,
  usePhysicalCount,
  useReceiveStock,
  useReorderInventoryItems,
  useUpdateInventoryItem,
} from '../queries';
import type { InventoryItem } from '../types';

type Panel = { mode: 'create' } | { mode: 'edit' | 'count' | 'receive'; item: InventoryItem };

const PAGE_TABS = [
  { id: 'ingredients', label: 'Ingredients' },
  { id: 'categories', label: 'Categories' },
] as const;
type PageTab = (typeof PAGE_TABS)[number]['id'];

export function IngredientsPage() {
  const items = useInventoryItems();
  const categories = useInventoryCategories();
  const { role } = useSession();
  const { branches } = useSelectableBranches();
  const [params, setParams] = useSearchParams();
  const [tab, setTab] = useState<PageTab>('ingredients');
  // null: all; '': uncategorised; otherwise a category id.
  const [filter, setFilter] = useState<string | null>(null);
  const [panel, setPanel] = useState<Panel>({ mode: 'create' });
  const reset = () => setPanel({ mode: 'create' });

  // Restock first links here with ?receive=<id> to open that ingredient's delivery form.
  const receiveId = params.get('receive');
  const receiveItem = receiveId ? items.data?.find((i) => i.id === receiveId) : undefined;
  useEffect(() => {
    if (!receiveItem) return;
    setPanel({ mode: 'receive', item: receiveItem });
    setParams({}, { replace: true });
  }, [receiveItem, setParams]);

  const reorder = useReorderInventoryItems();

  // One group per category in the shop's own order, then the uncategorised ones. Within a group, the saved order.
  const groups: SortableGroup<InventoryItem>[] = (() => {
    const byOrder = (a: InventoryItem, b: InventoryItem) => a.sortOrder - b.sortOrder || a.name.localeCompare(b.name);
    const rows = items.data ?? [];
    const known = new Set((categories.data ?? []).map((c) => c.id));
    const result: SortableGroup<InventoryItem>[] = [...(categories.data ?? [])]
      .sort((a, b) => a.sortOrder - b.sortOrder)
      .map((c) => ({ id: c.id, title: c.name, rows: rows.filter((i) => i.categoryId === c.id).sort(byOrder) }));
    result.push({ id: '', title: 'Uncategorised', rows: rows.filter((i) => i.categoryId === null || !known.has(i.categoryId)).sort(byOrder) });
    return result.filter((g) => g.rows.length > 0 && (filter === null || g.id === filter));
  })();

  const columns: SortableColumn<InventoryItem>[] = [
    {
      header: 'Ingredient',
      cell: (item) => (
        <div className="min-w-0">
          <p className="font-semibold text-gray-900">{item.name}</p>
          {item.isCountedByHand && <p className="max-w-xs text-xs text-gray-500">Not deducted when items sell. Use Count stock to update it, for example at the end of a shift.</p>}
        </div>
      ),
    },
    { header: 'On hand', className: 'tabular-nums', cell: (item) => `${item.quantityOnHand} ${item.baseUnit}` },
    {
      header: 'Packaging',
      className: 'text-gray-600',
      cell: (item) => `1 ${item.packagingUnit} = ${item.packagingSize} ${item.baseUnit}${item.lowStockThreshold !== null ? `, alert at ${item.lowStockThreshold}` : ''}`,
    },
    {
      header: 'Status',
      cell: (item) => (
        <div className="flex flex-wrap gap-1">
          {!item.isActive && <Pill>Inactive</Pill>}
          {item.isAutoCreatedForItem && <Pill tone="brand">Tracked with item</Pill>}
          {item.isCountedByHand && <Pill tone="warn">Counted by hand</Pill>}
        </div>
      ),
    },
    {
      header: '',
      className: 'text-right',
      cell: (item) => (
        <div className="flex flex-wrap justify-end gap-x-3 gap-y-1 text-xs">
          <button type="button" className="text-gray-500 hover:text-gray-900 hover:underline" onClick={() => setPanel({ mode: 'edit', item })}>
            Edit
          </button>
          <button type="button" className="text-gray-500 hover:text-gray-900 hover:underline" onClick={() => setPanel({ mode: 'count', item })}>
            Count stock
          </button>
          <button type="button" className="text-gray-500 hover:text-gray-900 hover:underline" onClick={() => setPanel({ mode: 'receive', item })}>
            Receive delivery
          </button>
        </div>
      ),
    },
  ];

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Ingredients"
        subtitle="Stock items you buy in bulk and use in recipes"
        backTo={{ to: '/inventory', label: 'Inventory' }}
      />
      <Tabs label="Ingredients section" tabs={PAGE_TABS} active={tab} onChange={setTab} idPrefix="ingredients-page" />
      {tab === 'categories' ? (
        <div id="ingredients-page-panel" role="tabpanel">
          <IngredientCategories canEdit={role === 'Admin' || role === 'Manager'} />
        </div>
      ) : (
      <div id="ingredients-page-panel" role="tabpanel" className="flex flex-col gap-4">
      {(categories.data ?? []).length > 0 && (
        <div className="flex flex-wrap gap-2" role="group" aria-label="Filter by category">
          {[{ id: null, label: 'All' }, ...(categories.data ?? []).map((c) => ({ id: c.id as string | null, label: c.name })), { id: '', label: 'Uncategorised' }].map((chip) => (
            <button
              key={chip.id ?? 'all'}
              type="button"
              aria-pressed={filter === chip.id}
              onClick={() => setFilter(chip.id)}
              className={`h-11 rounded-control px-4 text-base font-semibold ${filter === chip.id ? 'bg-brand text-on-brand' : 'border border-line bg-surface hover:border-brand'}`}
            >
              {chip.label}
            </button>
          ))}
        </div>
      )}
      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(0,26rem)]">
        <div className="min-w-0">
          {items.isPending ? (
            <SkeletonList />
          ) : items.isError ? (
            <ErrorState title="Ingredients could not be loaded" message={userMessage(items.error)} onRetry={() => void items.refetch()} />
          ) : groups.length === 0 ? (
            <p className="rounded-panel border border-dashed border-ink-soft/40 p-6 text-base text-ink-soft">No ingredients yet. Add one to start tracking it.</p>
          ) : (
            <SortableGroupedTable
              groups={groups}
              columns={columns}
              getId={(item) => item.id}
              rowLabel={(item) => item.name}
              disabled={reorder.isPending}
              onReorder={(ids) => reorder.mutate(ids, { onError: () => toast.error('Could not save the new order') })}
            />
          )}
        </div>

        {panel.mode === 'create' || panel.mode === 'edit' ? (
          <IngredientEditor key={panel.mode === 'edit' ? panel.item.id : 'new'} item={panel.mode === 'edit' ? panel.item : null} onDone={reset} />
        ) : panel.mode === 'count' ? (
          <CountPanel key={`count-${panel.item.id}`} item={panel.item} branches={branches ?? []} onDone={reset} />
        ) : (
          <ReceivePanel key={`receive-${panel.item.id}`} item={panel.item} branches={branches ?? []} onDone={reset} />
        )}
      </div>
      </div>
      )}
    </div>
  );
}

function IngredientEditor({ item, onDone }: { item: InventoryItem | null; onDone: () => void }) {
  const create = useCreateInventoryItem();
  const update = useUpdateInventoryItem();
  const categories = useInventoryCategories();
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<IngredientForm>({
    resolver: zodResolver(ingredientSchema),
    defaultValues: item
      ? {
          name: item.name,
          sku: item.sku ?? '',
          baseUnit: item.baseUnit,
          packagingUnit: item.packagingUnit,
          packagingSize: item.packagingSize,
          lowStockThreshold: item.lowStockThreshold === null ? '' : String(item.lowStockThreshold),
          categoryId: item.categoryId ?? '',
          isActive: item.isActive,
        }
      : { name: '', sku: '', baseUnit: '', packagingUnit: '', lowStockThreshold: '', categoryId: '', isActive: true },
  });

  const submit = handleSubmit(async (v) => {
    const threshold = parseThreshold(v.lowStockThreshold);
    if (!threshold.ok) {
      setError('lowStockThreshold', { message: threshold.message });
      return;
    }
    const body = {
      name: v.name,
      sku: v.sku.trim() || null,
      baseUnit: v.baseUnit,
      packagingUnit: v.packagingUnit,
      packagingSize: v.packagingSize,
      lowStockThreshold: threshold.value,
      categoryId: v.categoryId || null,
    };
    if (item) await update.mutateAsync({ id: item.id, body: { ...body, isActive: v.isActive } });
    else await create.mutateAsync(body);
    toast.success(item ? 'Ingredient updated' : 'Ingredient added');
    onDone();
  });

  return (
    <EditorCard title="ingredient" editing={!!item} busy={create.isPending || update.isPending} onSubmit={submit} onCancel={onDone}>
      <FormField label="Name" error={errors.name?.message}>
        <input {...register('name')} className={controlClass} />
      </FormField>
      <FormField label="SKU (optional)">
        <input {...register('sku')} className={controlClass} />
      </FormField>
      <FormField label="Base unit" hint="What you count in, like g, ml or pc" error={errors.baseUnit?.message}>
        <input {...register('baseUnit')} className={controlClass} />
      </FormField>
      <FormField label="Packaging unit" hint="How it is bought, like case, sack or box" error={errors.packagingUnit?.message}>
        <input {...register('packagingUnit')} className={controlClass} />
      </FormField>
      <FormField label="Packaging size" hint="Base units in one package" error={errors.packagingSize?.message}>
        <input type="number" inputMode="decimal" step="any" {...register('packagingSize', { valueAsNumber: true })} className={controlClass} />
      </FormField>
      <FormField label="Category (optional)">
        <select {...register('categoryId')} className={controlClass}>
          <option value="">Uncategorised</option>
          {(categories.data ?? []).map((c) => (
            <option key={c.id} value={c.id}>
              {c.name}
            </option>
          ))}
        </select>
      </FormField>
      <FormField label="Low stock alert (optional)" error={errors.lowStockThreshold?.message}>
        <input inputMode="decimal" {...register('lowStockThreshold')} className={controlClass} />
      </FormField>
      {item && (
        <label className="flex h-12 items-center gap-3 text-base font-semibold">
          <input type="checkbox" {...register('isActive')} className="size-6 accent-brand" />
          Active
        </label>
      )}
    </EditorCard>
  );
}

function BranchField({ branches, error, registration }: { branches: Branch[]; error?: string; registration: object }) {
  return (
    <FormField label="Branch" error={error}>
      <select {...registration} className={controlClass}>
        <option value="">Choose a branch</option>
        {branches.map((branch) => (
          <option key={branch.id} value={branch.id}>
            {branch.name}
          </option>
        ))}
      </select>
    </FormField>
  );
}

function CountPanel({ item, branches, onDone }: { item: InventoryItem; branches: Branch[]; onDone: () => void }) {
  const count = usePhysicalCount();
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<CountForm>({ resolver: zodResolver(countSchema), defaultValues: { branchId: branches.length === 1 ? branches[0].id : '' } });

  const submit = handleSubmit(async (v) => {
    await count.mutateAsync({ id: item.id, body: { quantityOnHand: v.quantityOnHand, branchId: v.branchId } });
    toast.success(`Count saved for ${item.name}`);
    onDone();
  });

  return (
    <EditorCard title="count" heading={`Count stock: ${item.name}`} cancelable editing={false} submitLabel="Save count" busy={count.isPending} onSubmit={submit} onCancel={onDone}>
      <p className="text-base text-ink-soft">
        Enter what is really on the shelf. The system records the difference as an adjustment.
      </p>
      <BranchField branches={branches} error={errors.branchId?.message} registration={register('branchId')} />
      <FormField label={`Counted quantity (${item.baseUnit})`} error={errors.quantityOnHand?.message}>
        <input type="number" inputMode="decimal" step="any" {...register('quantityOnHand', { valueAsNumber: true })} className={controlClass} />
      </FormField>
    </EditorCard>
  );
}

function ReceivePanel({ item, branches, onDone }: { item: InventoryItem; branches: Branch[]; onDone: () => void }) {
  const receive = useReceiveStock();
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<ReceiveForm>({
    resolver: zodResolver(receiveSchema),
    defaultValues: { branchId: branches.length === 1 ? branches[0].id : '', supplierReference: '' },
  });

  const submit = handleSubmit(async (v) => {
    await receive.mutateAsync({
      id: item.id,
      body: { packagesReceived: v.packagesReceived, branchId: v.branchId, supplierReference: v.supplierReference.trim() || null },
    });
    toast.success(`Delivery received for ${item.name}`);
    onDone();
  });

  return (
    <EditorCard title="delivery" heading={`Receive delivery: ${item.name}`} cancelable editing={false} submitLabel="Receive delivery" busy={receive.isPending} onSubmit={submit} onCancel={onDone}>
      <BranchField branches={branches} error={errors.branchId?.message} registration={register('branchId')} />
      <FormField
        label={`Packages received (${item.packagingUnit})`}
        hint={`Each ${item.packagingUnit} is ${item.packagingSize} ${item.baseUnit}`}
        error={errors.packagesReceived?.message}
      >
        <input type="number" inputMode="decimal" step="any" {...register('packagesReceived', { valueAsNumber: true })} className={controlClass} />
      </FormField>
      <FormField label="Supplier reference (optional)">
        <input {...register('supplierReference')} className={controlClass} />
      </FormField>
    </EditorCard>
  );
}
