import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { toast } from '../../../components/feedback/toastStore';
import { EditorCard } from '../../../components/forms/EditorCard';
import { FormField, controlClass } from '../../../components/forms/FormField';
import { ListCard, Pill, QueryList } from '../../../components/lists/QueryList';
import { PageHeader } from '../../../components/PageHeader';
import type { Branch } from '../../branches/types';
import { useSelectableBranches } from '../../branches/queries';
import { countSchema, ingredientSchema, parseThreshold, receiveSchema, type CountForm, type IngredientForm, type ReceiveForm } from '../ingredient';
import {
  useCreateInventoryItem,
  useInventoryItems,
  usePhysicalCount,
  useReceiveStock,
  useUpdateInventoryItem,
} from '../queries';
import type { InventoryItem } from '../types';

type Panel = { mode: 'create' } | { mode: 'edit' | 'count' | 'receive'; item: InventoryItem };

const linkButton = 'h-12 text-base font-semibold text-brand-strong underline';

export function IngredientsPage() {
  const items = useInventoryItems();
  const { branches } = useSelectableBranches();
  const [panel, setPanel] = useState<Panel>({ mode: 'create' });
  const reset = () => setPanel({ mode: 'create' });

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Ingredients"
        subtitle="Stock items you buy in bulk and use in recipes"
        backTo={{ to: '/inventory', label: 'Inventory' }}
      />
      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(0,26rem)]">
        <QueryList
          query={items}
          errorTitle="Ingredients could not be loaded"
          emptyMessage="No ingredients yet. Add one to start tracking it."
          renderRow={(item) => (
            <ListCard key={item.id}>
              <div className="min-w-0">
                <p className="text-base font-semibold">{item.name}</p>
                <p className="text-base tabular-nums">
                  {item.quantityOnHand} {item.baseUnit} on hand
                </p>
                <p className="text-sm text-ink-soft">
                  1 {item.packagingUnit} = {item.packagingSize} {item.baseUnit}
                  {item.lowStockThreshold !== null && `, alert at ${item.lowStockThreshold}`}
                </p>
                <div className="mt-2 flex flex-wrap gap-x-5">
                  <button type="button" className={linkButton} onClick={() => setPanel({ mode: 'edit', item })}>
                    Edit
                  </button>
                  <button type="button" className={linkButton} onClick={() => setPanel({ mode: 'count', item })}>
                    Count stock
                  </button>
                  <button type="button" className={linkButton} onClick={() => setPanel({ mode: 'receive', item })}>
                    Receive delivery
                  </button>
                </div>
              </div>
              <div className="flex flex-col items-end gap-2">
                {!item.isActive && <Pill>Inactive</Pill>}
                {item.isAutoCreatedForItem && <Pill tone="brand">Tracked with item</Pill>}
              </div>
            </ListCard>
          )}
        />

        {panel.mode === 'create' || panel.mode === 'edit' ? (
          <IngredientEditor key={panel.mode === 'edit' ? panel.item.id : 'new'} item={panel.mode === 'edit' ? panel.item : null} onDone={reset} />
        ) : panel.mode === 'count' ? (
          <CountPanel key={`count-${panel.item.id}`} item={panel.item} branches={branches ?? []} onDone={reset} />
        ) : (
          <ReceivePanel key={`receive-${panel.item.id}`} item={panel.item} branches={branches ?? []} onDone={reset} />
        )}
      </div>
    </div>
  );
}

function IngredientEditor({ item, onDone }: { item: InventoryItem | null; onDone: () => void }) {
  const create = useCreateInventoryItem();
  const update = useUpdateInventoryItem();
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
          isActive: item.isActive,
        }
      : { name: '', sku: '', baseUnit: '', packagingUnit: '', lowStockThreshold: '', isActive: true },
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
