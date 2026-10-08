import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { toast } from '../../../components/feedback/toastStore';
import { FormField, PrimaryButton, SecondaryButton, controlClass } from '../../../components/forms/FormField';
import { Modal } from '../../../components/Modal';
import { StockBranchPicker, StockStepper, useStockBranch } from '../../../components/StockStepper';
import { SearchBar } from '../../../components/SearchBar';
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
import { ingredientSchema, parseThreshold, receiveSchema, type IngredientForm, type ReceiveForm } from '../ingredient';
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
import { RowActionsMenu } from '../../../components/RowActionsMenu';
import { DeletedRecordsPanel, StatusFilter, type StatusView } from '../../lifecycle/StatusFilter';
import { useLifecycle } from '../../lifecycle/useLifecycle';

type Dialog = { mode: 'create' } | { mode: 'edit' | 'receive'; item: InventoryItem };

const PAGE_TABS = [
  { id: 'ingredients', label: 'Ingredients' },
  { id: 'categories', label: 'Categories' },
] as const;
type PageTab = (typeof PAGE_TABS)[number]['id'];

// Option values for the category dropdown ('' is a real filter: ingredients with no category).
const ALL = 'all';
const NONE = 'none';

export function IngredientsPage() {
  const items = useInventoryItems();
  const categories = useInventoryCategories();
  const { role } = useSession();
  const { branches } = useSelectableBranches();
  const [params, setParams] = useSearchParams();
  const [tab, setTab] = useState<PageTab>('ingredients');
  // null: all; '': uncategorised; otherwise a category id.
  const [filter, setFilter] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [view, setView] = useState<StatusView>('active');
  const { run, dialog: lifecycleDialog } = useLifecycle();
  const searching = search.trim() !== '';
  const [dialog, setDialog] = useState<Dialog | null>(null);
  const close = () => setDialog(null);
  const stockBranch = useStockBranch();
  const physicalCount = usePhysicalCount();

  function setStock(item: InventoryItem, next: number) {
    if (!stockBranch.branchId) {
      toast.error('No branch is available to record this stock change against.');
      return;
    }
    return physicalCount.mutateAsync({ id: item.id, body: { quantityOnHand: next, branchId: stockBranch.branchId } });
  }

  // Restock first links here with ?receive=<id> to open that ingredient's delivery form.
  const receiveId = params.get('receive');
  const receiveItem = receiveId ? items.data?.find((i) => i.id === receiveId) : undefined;
  useEffect(() => {
    if (!receiveItem) return;
    setDialog({ mode: 'receive', item: receiveItem });
    setParams({}, { replace: true });
  }, [receiveItem, setParams]);

  const reorder = useReorderInventoryItems();

  // One group per category in the shop's own order, then the uncategorised ones. Within a group, the saved order.
  const groups: SortableGroup<InventoryItem>[] = (() => {
    const byOrder = (a: InventoryItem, b: InventoryItem) => a.sortOrder - b.sortOrder || a.name.localeCompare(b.name);
    // An item's own paired stock record is managed on the Items page, not here.
    const q = search.trim().toLowerCase();
    const rows = (items.data ?? []).filter((i) => !i.isAutoCreatedForItem && (view === 'active' ? i.isActive : !i.isActive) && (q === '' || i.name.toLowerCase().includes(q) || (i.sku?.toLowerCase().includes(q) ?? false)));
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
          {item.isCountedByHand && <p className="max-w-xs text-xs text-gray-500">Not deducted when items sell. Update the count by hand, for example at the end of a shift.</p>}
        </div>
      ),
    },
    {
      header: 'On hand',
      cell: (item) => <StockStepper value={item.quantityOnHand} unit={item.baseUnit} label={item.name} disabled={physicalCount.isPending || !stockBranch.branchId} onSet={(next) => setStock(item, next)} />,
    },
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
          {item.isCountedByHand && <Pill tone="warn">Counted by hand</Pill>}
        </div>
      ),
    },
    {
      header: '',
      className: 'text-right',
      cell: (item) => (
        <div className="flex justify-end">
          <RowActionsMenu
            subject={item.name}
            actions={[
              { label: 'Edit', onSelect: () => setDialog({ mode: 'edit', item }) },
              { label: 'Receive delivery', onSelect: () => setDialog({ mode: 'receive', item }) },
              item.isActive
                ? { label: 'Make inactive', onSelect: () => run({ kind: 'Ingredient', id: item.id, name: item.name }, 'deactivate'), separated: true }
                : { label: 'Make active', onSelect: () => run({ kind: 'Ingredient', id: item.id, name: item.name }, 'reactivate'), separated: true },
              { label: 'Delete', danger: true, onSelect: () => run({ kind: 'Ingredient', id: item.id, name: item.name }, 'delete') },
            ]}
          />
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
        action={
          tab === 'ingredients' ? (
            <button type="button" onClick={() => setDialog({ mode: 'create' })} className="h-12 rounded-control bg-brand px-5 text-base font-semibold text-on-brand hover:bg-brand-strong">
              Add Ingredient
            </button>
          ) : undefined
        }
      />
      <Tabs label="Ingredients section" tabs={PAGE_TABS} active={tab} onChange={setTab} idPrefix="ingredients-page" />
      {tab === 'categories' ? (
        <div id="ingredients-page-panel" role="tabpanel">
          <IngredientCategories canEdit={role === 'Admin' || role === 'Manager'} />
        </div>
      ) : (
      <div id="ingredients-page-panel" role="tabpanel" className="flex flex-col gap-4">
      <StatusFilter value={view} onChange={setView} />
      {view === 'deleted' ? (
        <DeletedRecordsPanel kind="Ingredient" noun="ingredients" />
      ) : (
      <>
      <div className="flex flex-wrap items-center gap-3">
        <SearchBar value={search} onChange={setSearch} placeholder="Search ingredients…" />
        {(categories.data ?? []).length > 0 && (
          <select
            aria-label="Filter by category"
            value={filter === null ? ALL : filter === '' ? NONE : filter}
            onChange={(e) => setFilter(e.target.value === ALL ? null : e.target.value === NONE ? '' : e.target.value)}
            className="rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-gray-500 focus:outline-none"
          >
            <option value={ALL}>All categories</option>
            {(categories.data ?? []).map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
            <option value={NONE}>Uncategorised</option>
          </select>
        )}
      </div>
      {searching && <p className="text-xs text-gray-500">Clear the search to change the order of ingredients.</p>}
      <StockBranchPicker branches={stockBranch.branches} branchId={stockBranch.branchId} onChange={stockBranch.setBranchId} />
        <div className="min-w-0">
          {items.isPending ? (
            <SkeletonList />
          ) : items.isError ? (
            <ErrorState title="Ingredients could not be loaded" message={userMessage(items.error)} onRetry={() => void items.refetch()} />
          ) : groups.length === 0 ? (
            <p className="rounded-panel border border-dashed border-ink-soft/40 p-6 text-base text-ink-soft">
              {searching ? 'No ingredients match your search.' : 'No ingredients yet. Add one to start tracking it.'}
            </p>
          ) : (
            <SortableGroupedTable
              groups={groups}
              columns={columns}
              getId={(item) => item.id}
              rowLabel={(item) => item.name}
              disabled={reorder.isPending || searching}
              onReorder={(ids) => reorder.mutate(ids, { onError: () => toast.error('Could not save the new order') })}
            />
          )}
        </div>
      </>
      )}
      </div>
      )}
      {lifecycleDialog}
      {dialog &&
        (dialog.mode === 'receive' ? (
          <ReceiveDialog item={dialog.item} branches={branches ?? []} onDone={close} />
        ) : (
          <IngredientDialog item={dialog.mode === 'edit' ? dialog.item : null} onDone={close} />
        ))}
    </div>
  );
}

function IngredientDialog({ item, onDone }: { item: InventoryItem | null; onDone: () => void }) {
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
    <Modal
      open
      title={item ? 'Edit Ingredient' : 'Add Ingredient'}
      onClose={onDone}
      footer={
        <div className="flex justify-end gap-3">
          <SecondaryButton type="button" onClick={onDone}>
            Cancel
          </SecondaryButton>
          <PrimaryButton type="submit" form="ingredient-dialog-form" busy={create.isPending || update.isPending}>
            {create.isPending || update.isPending ? 'Saving...' : item ? 'Save changes' : 'Add'}
          </PrimaryButton>
        </div>
      }
    >
      <form id="ingredient-dialog-form" onSubmit={submit} noValidate className="flex flex-col gap-4">
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
      </form>
    </Modal>
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

function ReceiveDialog({ item, branches, onDone }: { item: InventoryItem; branches: Branch[]; onDone: () => void }) {
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
    <Modal
      open
      title={`Receive delivery: ${item.name}`}
      onClose={onDone}
      footer={
        <div className="flex justify-end gap-3">
          <SecondaryButton type="button" onClick={onDone}>
            Cancel
          </SecondaryButton>
          <PrimaryButton type="submit" form="receive-dialog-form" busy={receive.isPending}>
            {receive.isPending ? 'Saving...' : 'Receive delivery'}
          </PrimaryButton>
        </div>
      }
    >
      <form id="receive-dialog-form" onSubmit={submit} noValidate className="flex flex-col gap-4">
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
      </form>
    </Modal>
  );
}
