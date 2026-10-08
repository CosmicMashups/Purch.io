import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import type { z } from 'zod';
import { modifierGroupSchema, modifierSchema } from '../schemas';
import { useAddModifier, useAttachModifierGroupToItems, useCategories, useItems, useCreateModifierGroup, useModifierGroups, useReplaceModifierIngredients, useUpdateModifier, useUpdateModifierCategoryItem, useUpdateModifierGroup } from '../queries';
import { Field, inputClass } from '../../../components/Field';
import { FormDialog } from '../../../components/forms/FormDialog';
import { FormField, PrimaryButton, controlClass } from '../../../components/forms/FormField';
import { EmptyState } from '../../../components/EmptyState';
import { ErrorState, describeQueryError } from '../../../components/ErrorState';
import { SkeletonList } from '../../../components/Skeleton';
import { toast } from '../../../components/feedback/toastStore';
import { userMessage } from '../../../lib/apiError';
import { useInventoryItems } from '../../inventory/queries';
import { useTenantSettings } from '../../tenant/queries';
import { IngredientSelector } from '../components/IngredientSelector';
import { buildRecipeLines, selectionFromRecipe, type RecipeSelection } from '../recipe';
import type { Modifier, ModifierCategoryItem, ModifierGroup } from '../types';
import { useState } from 'react';
import { PageHeader } from '../../../components/PageHeader';
import { RowActionsMenu } from '../../../components/RowActionsMenu';
import { StatusBadge } from '../../../components/StatusBadge';
import { DeletedRecordsPanel, StatusFilter, type StatusView } from '../../lifecycle/StatusFilter';
import { useLifecycle } from '../../lifecycle/useLifecycle';

type GroupFormValues = z.infer<typeof modifierGroupSchema>;
type ModifierFormValues = z.infer<typeof modifierSchema>;

export function ModifierGroupsPage() {
  const { data: groups, isLoading, isError, error, refetch } = useModifierGroups();
  // null: closed; 'new': adding; otherwise the group being edited.
  const [groupDialog, setGroupDialog] = useState<'new' | ModifierGroup | null>(null);
  // The group a modifier belongs to, and the modifier being edited (null when adding).
  const [modifierDialog, setModifierDialog] = useState<{ groupId: string; modifier: Modifier | null } | null>(null);
  const [categoryGroupId, setCategoryGroupId] = useState<string | null>(null);
  const [applyGroupId, setApplyGroupId] = useState<string | null>(null);
  const [view, setView] = useState<StatusView>('active');
  const categories = useCategories();
  const { run, dialog } = useLifecycle();
  // Ingredients only mean something when the business tracks its stock as ingredients.
  const tracksIngredients = useTenantSettings().data?.useSeparateInventoryTracking === true;

  const shown = (groups ?? []).filter((g) => (view === 'active' ? g.isActive !== false : g.isActive === false));

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Modifier groups"
        subtitle="Extras and choices customers can add to an item."
        backTo={{ to: '/business', label: 'Business' }}
        action={
          <PrimaryButton type="button" onClick={() => setGroupDialog('new')}>
            Add group
          </PrimaryButton>
        }
      />
      <StatusFilter value={view} onChange={setView} />

      {view === 'deleted' ? (
        <DeletedRecordsPanel kind="ModifierGroup" noun="modifier groups" />
      ) : (
        <>
          {isLoading && <SkeletonList />}
          {isError && <ErrorState message={describeQueryError(error)} onRetry={() => refetch()} />}
          {!isLoading && !isError && shown.length === 0 && <EmptyState title={view === 'active' ? 'No modifier groups yet' : 'No inactive modifier groups'} />}
          <ul className="flex flex-col gap-3">
            {!isError &&
              shown.map((g) => (
                <li key={g.id} className="rounded-panel border border-line bg-surface p-4">
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0">
                      <p className="flex flex-wrap items-center gap-2 text-lg font-semibold">
                        {g.name}
                        {g.isActive === false && <StatusBadge label="Inactive" tone="warning" />}
                      </p>
                      <p className="text-sm text-ink-soft">
                        {g.allowMultipleSelection ? 'Choose several' : 'Choose one'} · {g.isRequired ? 'Required' : 'Optional'}
                      </p>
                    </div>
                    <div className="flex shrink-0 items-center gap-1">
                      <button
                        type="button"
                        aria-label={`Add modifier to ${g.name}`}
                        onClick={() => setModifierDialog({ groupId: g.id, modifier: null })}
                        className="hidden h-12 items-center rounded-control border border-line px-4 text-base font-semibold hover:border-brand sm:inline-flex"
                      >
                        Add modifier
                      </button>
                      <RowActionsMenu
                        subject={g.name}
                        actions={[
                          { label: 'Edit group', onSelect: () => setGroupDialog(g) },
                          { label: 'Add modifier', onSelect: () => setModifierDialog({ groupId: g.id, modifier: null }) },
                          { label: 'Offer a category', onSelect: () => setCategoryGroupId(categoryGroupId === g.id ? null : g.id) },
                          { label: 'Apply to items', onSelect: () => setApplyGroupId(applyGroupId === g.id ? null : g.id) },
                          g.isActive === false
                            ? { label: 'Make active', onSelect: () => run({ kind: 'ModifierGroup', id: g.id, name: g.name }, 'reactivate'), separated: true }
                            : { label: 'Make inactive', onSelect: () => run({ kind: 'ModifierGroup', id: g.id, name: g.name }, 'deactivate'), separated: true },
                          { label: 'Delete', danger: true, onSelect: () => run({ kind: 'ModifierGroup', id: g.id, name: g.name }, 'delete') },
                        ]}
                      />
                    </div>
                  </div>

                  {g.categoryId && (
                    <p className="mt-1 text-sm text-ink-soft">
                      Also offers every item in {categories.data?.find((c) => c.id === g.categoryId)?.name ?? 'its category'}
                    </p>
                  )}
                  {categoryGroupId === g.id && <CategoryPanel group={g} categories={categories.data ?? []} />}
                  {applyGroupId === g.id && <ApplyToItemsPanel group={g} categories={categories.data ?? []} />}

                  {g.modifiers.length > 0 && (
                    <ul className="mt-2 flex flex-col divide-y divide-line" aria-label={`${g.name} modifiers`}>
                      {g.modifiers.map((m) => (
                        <ModifierRow
                          key={m.id}
                          modifier={m}
                          tracksIngredients={tracksIngredients}
                          onEdit={() => setModifierDialog({ groupId: g.id, modifier: m })}
                          onLifecycle={(action) => run({ kind: 'Modifier', id: m.id, name: m.name }, action)}
                        />
                      ))}
                    </ul>
                  )}
                </li>
              ))}
          </ul>
        </>
      )}

      {groupDialog && <GroupDialog group={groupDialog === 'new' ? null : groupDialog} categories={categories.data ?? []} onClose={() => setGroupDialog(null)} />}
      {modifierDialog && <ModifierDialog groupId={modifierDialog.groupId} modifier={modifierDialog.modifier} onClose={() => setModifierDialog(null)} />}
      {dialog}
    </div>
  );
}

/** One modifier: its price and, when stock is tracked, what it uses up. Tap it to edit; the rest is in the menu. */
function ModifierRow({
  modifier,
  tracksIngredients,
  onEdit,
  onLifecycle,
}: {
  modifier: Modifier;
  tracksIngredients: boolean;
  onEdit: () => void;
  onLifecycle: (action: 'deactivate' | 'reactivate' | 'delete') => void;
}) {
  const [panel, setPanel] = useState<'ingredients' | null>(null);
  const ingredientNames = (modifier.ingredients ?? []).map((i) => i.inventoryItemName);
  const inactive = modifier.isActive === false;

  return (
    <li className="py-1">
      <div className="flex items-center justify-between gap-2">
        <button type="button" onClick={onEdit} aria-label={`Edit ${modifier.name}`} className="min-h-12 min-w-0 flex-1 rounded-control px-2 text-left hover:bg-canvas">
          <span className="flex flex-wrap items-center gap-2 text-base font-semibold">
            {modifier.name}
            <span className="font-normal text-ink-soft">
              ({modifier.priceDelta < 0 ? '-' : '+'}₱{Math.abs(modifier.priceDelta).toFixed(2)})
            </span>
            {modifier.isOutOfStock && <StatusBadge label="Sold out" tone="danger" />}
            {inactive && <StatusBadge label="Inactive" tone="warning" />}
          </span>
          {tracksIngredients && <span className="block text-sm text-ink-soft">{ingredientNames.length > 0 ? `Uses ${ingredientNames.join(', ')}` : 'Uses no ingredients'}</span>}
        </button>
        <RowActionsMenu
          subject={modifier.name}
          actions={[
            { label: 'Edit', onSelect: onEdit },
            ...(tracksIngredients ? [{ label: panel === 'ingredients' ? 'Hide ingredients' : 'Ingredients', onSelect: () => setPanel(panel === 'ingredients' ? null : 'ingredients') }] : []),
            inactive
              ? { label: 'Make active', onSelect: () => onLifecycle('reactivate'), separated: true }
              : { label: 'Make inactive', onSelect: () => onLifecycle('deactivate'), separated: true },
            { label: 'Delete', danger: true, onSelect: () => onLifecycle('delete') },
          ]}
        />
      </div>
      {panel === 'ingredients' && <ModifierIngredientsEditor modifier={modifier} onDone={() => setPanel(null)} />}
    </li>
  );
}

/** The inventory a modifier takes when it is chosen, set the same way an item's recipe is. */
function ModifierIngredientsEditor({ modifier, onDone }: { modifier: Modifier; onDone: () => void }) {
  const inventory = useInventoryItems();
  const save = useReplaceModifierIngredients(modifier.id);
  const [selection, setSelection] = useState<RecipeSelection>(() =>
    selectionFromRecipe((modifier.ingredients ?? []).map((i) => ({ inventoryItemId: i.inventoryItemId, inventoryItemName: i.inventoryItemName, quantityPerOrder: i.quantityPerOrder }))),
  );
  const [lineError, setLineError] = useState<{ id: string; message: string } | null>(null);

  async function onSave() {
    const result = buildRecipeLines(selection);
    if (!result.ok) {
      setLineError({ id: result.inventoryItemId, message: result.message });
      return;
    }
    try {
      await save.mutateAsync({ lines: result.lines });
      toast.success(`${modifier.name} ingredients saved`);
      onDone();
    } catch (error) {
      toast.error(userMessage(error));
    }
  }

  if (inventory.isPending) return <p className="mt-2 text-sm text-gray-500">Loading ingredients…</p>;
  if (inventory.isError) return <ErrorState message={describeQueryError(inventory.error)} onRetry={() => void inventory.refetch()} />;
  if (inventory.data.length === 0) return <p className="mt-2 text-sm text-gray-500">Add ingredients in Inventory first, then choose which ones this option uses.</p>;

  return (
    <div className="mt-2 flex max-w-lg flex-col gap-3 rounded-md bg-gray-50 p-3">
      <IngredientSelector
        subject="modifier"
        ingredients={inventory.data}
        selection={selection}
        onChange={(next) => {
          setLineError(null);
          setSelection(next);
        }}
        lineError={lineError}
      />
      <button type="button" onClick={() => void onSave()} disabled={save.isPending} className="self-start rounded-md bg-gray-900 px-3 py-1.5 text-sm font-medium text-white disabled:opacity-50">
        {save.isPending ? 'Saving…' : 'Save ingredients'}
      </button>
    </div>
  );
}

/** Adds a modifier to a group, or edits one. */
function ModifierDialog({ groupId, modifier, onClose }: { groupId: string; modifier: Modifier | null; onClose: () => void }) {
  const add = useAddModifier(groupId);
  const update = useUpdateModifier();
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<ModifierFormValues>({
    resolver: zodResolver(modifierSchema),
    defaultValues: modifier ? { name: modifier.name, priceDelta: modifier.priceDelta } : { name: '', priceDelta: 0 },
  });

  async function onSubmit(values: ModifierFormValues) {
    try {
      if (modifier) await update.mutateAsync({ modifierId: modifier.id, body: values });
      else await add.mutateAsync(values);
      onClose();
    } catch (error) {
      toast.error(userMessage(error));
    }
  }

  return (
    <FormDialog
      title={modifier ? `Edit ${modifier.name}` : 'Add modifier'}
      submitLabel={modifier ? 'Save changes' : 'Add'}
      busy={add.isPending || update.isPending}
      onSubmit={handleSubmit(onSubmit)}
      onClose={onClose}
    >
      <FormField label="Modifier name" error={errors.name?.message}>
        <input {...register('name')} className={controlClass} />
      </FormField>
      <FormField label="Price delta" hint="Added to the price when chosen. Use a minus for a discount." error={errors.priceDelta?.message}>
        <input type="number" step="0.01" inputMode="decimal" {...register('priceDelta')} className={controlClass} />
      </FormField>
    </FormDialog>
  );
}

/** Adds a modifier group, or edits its name, rules and category. */
function GroupDialog({ group, categories, onClose }: { group: ModifierGroup | null; categories: { id: string; name: string }[]; onClose: () => void }) {
  const create = useCreateModifierGroup();
  const update = useUpdateModifierGroup(group?.id ?? '');
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<GroupFormValues>({
    resolver: zodResolver(modifierGroupSchema),
    defaultValues: group
      ? { name: group.name, allowMultipleSelection: group.allowMultipleSelection, isRequired: group.isRequired, categoryId: group.categoryId ?? null }
      : { name: '', allowMultipleSelection: false, isRequired: false, categoryId: null },
  });

  async function onSubmit(values: GroupFormValues) {
    try {
      if (group) await update.mutateAsync({ ...values, categoryId: values.categoryId ?? null });
      else await create.mutateAsync(values);
      toast.success(group ? 'Group updated' : 'Group added');
      onClose();
    } catch (error) {
      toast.error(userMessage(error));
    }
  }

  return (
    <FormDialog
      title={group ? `Edit ${group.name}` : 'Add modifier group'}
      submitLabel={group ? 'Save changes' : 'Add Group'}
      busy={create.isPending || update.isPending}
      onSubmit={handleSubmit(onSubmit)}
      onClose={onClose}
    >
      <FormField label="Group name" error={errors.name?.message}>
        <input {...register('name')} className={controlClass} placeholder="Spice Level" />
      </FormField>
      <label className="flex h-12 items-center gap-3 text-base font-semibold">
        <input type="checkbox" {...register('allowMultipleSelection')} className="size-6 accent-brand" />
        Allow multiple selection
      </label>
      <label className="flex h-12 items-center gap-3 text-base font-semibold">
        <input type="checkbox" {...register('isRequired')} className="size-6 accent-brand" />
        Required
      </label>
      <FormField label="Offer items from a category (optional)" error={errors.categoryId?.message}>
        <select {...register('categoryId')} className={controlClass}>
          <option value="">None, only the modifiers below</option>
          {categories.map((category) => (
            <option key={category.id} value={category.id}>
              {category.name}
            </option>
          ))}
        </select>
      </FormField>
    </FormDialog>
  );
}

/** Gives the group to every item of a category, or to the items ticked, so it need not be attached item by item. */
function ApplyToItemsPanel({ group, categories }: { group: ModifierGroup; categories: { id: string; name: string }[] }) {
  const attach = useAttachModifierGroupToItems(group.id);
  const items = useItems();
  const [mode, setMode] = useState<'category' | 'items'>('category');
  const [categoryId, setCategoryId] = useState('');
  const [chosen, setChosen] = useState<Set<string>>(new Set());
  const [search, setSearch] = useState('');

  const activeItems = (items.data ?? []).filter((item) => item.isActive);
  const shown = activeItems.filter((item) => item.name.toLowerCase().includes(search.trim().toLowerCase()));
  const categoryCount = activeItems.filter((item) => item.categoryId === categoryId).length;
  const ready = mode === 'category' ? categoryId !== '' && categoryCount > 0 : chosen.size > 0;

  function toggle(id: string) {
    setChosen((current) => {
      const next = new Set(current);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  async function onApply() {
    try {
      const result = await attach.mutateAsync(mode === 'category' ? { categoryId } : { itemIds: [...chosen] });
      toast.success(
        result.alreadyAttached > 0
          ? `${group.name} added to ${result.attached} items; ${result.alreadyAttached} already had it`
          : `${group.name} added to ${result.attached} items`,
      );
      setChosen(new Set());
    } catch (error) {
      toast.error(userMessage(error));
    }
  }

  return (
    <div className="mt-2 flex max-w-lg flex-col gap-3 rounded-md bg-gray-50 p-3">
      <fieldset className="flex flex-col gap-1 text-sm text-gray-700">
        <legend className="mb-1 font-medium">Give {group.name} to</legend>
        <label className="flex items-center gap-2">
          <input type="radio" name={`apply-${group.id}`} checked={mode === 'category'} onChange={() => setMode('category')} />
          All items of a category
        </label>
        <label className="flex items-center gap-2">
          <input type="radio" name={`apply-${group.id}`} checked={mode === 'items'} onChange={() => setMode('items')} />
          Selected items
        </label>
      </fieldset>

      {mode === 'category' ? (
        <Field label="Category">
          <select value={categoryId} onChange={(e) => setCategoryId(e.target.value)} className={inputClass} aria-label={`Category to give ${group.name} to`}>
            <option value="">Choose a category</option>
            {categories.map((category) => (
              <option key={category.id} value={category.id}>
                {category.name}
              </option>
            ))}
          </select>
        </Field>
      ) : (
        <div className="flex flex-col gap-2">
          <input value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Search items" aria-label="Search items" className={inputClass} />
          <ul className="flex max-h-56 flex-col divide-y divide-gray-200 overflow-y-auto rounded-md border border-gray-200 bg-white" aria-label={`Items to give ${group.name} to`}>
            {shown.length === 0 && <li className="px-3 py-2 text-sm text-gray-500">No items found.</li>}
            {shown.map((item) => (
              <li key={item.id}>
                <label className="flex items-center gap-2 px-3 py-2 text-sm text-gray-900">
                  <input type="checkbox" checked={chosen.has(item.id)} onChange={() => toggle(item.id)} />
                  {item.name}
                </label>
              </li>
            ))}
          </ul>
        </div>
      )}

      <p className="text-xs text-gray-500">
        {mode === 'category'
          ? categoryId === ''
            ? 'Every active item in the category gets the group. Items that already have it are left alone.'
            : `${categoryCount} active ${categoryCount === 1 ? 'item' : 'items'} in this category.`
          : `${chosen.size} selected.`}
      </p>
      <button type="button" onClick={() => void onApply()} disabled={!ready || attach.isPending} className="self-start rounded-md bg-gray-900 px-3 py-1.5 text-sm font-medium text-white disabled:opacity-50">
        {attach.isPending ? 'Applying…' : 'Apply'}
      </button>
    </div>
  );
}

/** Links the group to a category (or unlinks it) and tunes each of that category's items for this group. */
function CategoryPanel({ group, categories }: { group: ModifierGroup; categories: { id: string; name: string }[] }) {
  const update = useUpdateModifierGroup(group.id);
  const [categoryId, setCategoryId] = useState(group.categoryId ?? '');

  async function onSave() {
    try {
      await update.mutateAsync({
        name: group.name,
        allowMultipleSelection: group.allowMultipleSelection,
        isRequired: group.isRequired,
        categoryId: categoryId === '' ? null : categoryId,
      });
      toast.success('Category saved');
    } catch (error) {
      toast.error(userMessage(error));
    }
  }

  return (
    <div className="mt-2 flex flex-col gap-3 rounded-md bg-gray-50 p-3">
      <div className="flex max-w-md flex-wrap items-end gap-3">
        <Field label={`Category offered in ${group.name}`}>
          <select value={categoryId} onChange={(e) => setCategoryId(e.target.value)} className={inputClass}>
            <option value="">None</option>
            {categories.map((category) => (
              <option key={category.id} value={category.id}>
                {category.name}
              </option>
            ))}
          </select>
        </Field>
        <button
          type="button"
          onClick={() => void onSave()}
          disabled={update.isPending || categoryId === (group.categoryId ?? '')}
          className="rounded-md bg-gray-900 px-3 py-1.5 text-sm font-medium text-white disabled:opacity-50"
        >
          {update.isPending ? 'Saving…' : 'Save category'}
        </button>
      </div>
      {group.categoryId && (
        <>
          <p className="text-xs text-gray-500">
            Every active item in the category is offered at its own price. Set a price here to charge something else in this group, or hide an item from it. Changing the category clears these.
          </p>
          {(group.categoryItems ?? []).length === 0 && <p className="text-sm text-gray-500">This category has no active items yet.</p>}
          <ul className="flex flex-col divide-y divide-gray-200" aria-label={`${group.name} category items`}>
            {(group.categoryItems ?? []).map((entry) => (
              <CategoryItemRow key={`${entry.itemId}:${entry.priceOverride}:${entry.isExcluded}`} groupId={group.id} entry={entry} />
            ))}
          </ul>
        </>
      )}
    </div>
  );
}

function CategoryItemRow({ groupId, entry }: { groupId: string; entry: ModifierCategoryItem }) {
  const update = useUpdateModifierCategoryItem(groupId);
  const [price, setPrice] = useState(entry.priceOverride === null ? '' : String(entry.priceOverride));
  const [hidden, setHidden] = useState(entry.isExcluded);
  const changed = price !== (entry.priceOverride === null ? '' : String(entry.priceOverride)) || hidden !== entry.isExcluded;

  async function onSave() {
    try {
      await update.mutateAsync({ itemId: entry.itemId, body: { priceOverride: price.trim() === '' ? null : Number(price), isExcluded: hidden } });
    } catch (error) {
      toast.error(userMessage(error));
    }
  }

  return (
    <li className="flex flex-wrap items-center gap-3 py-2">
      <span className="min-w-40 flex-1 text-sm font-medium text-gray-900">
        {entry.name} <span className="font-normal text-gray-600">(₱{entry.basePrice.toFixed(2)})</span>
      </span>
      <label className="flex items-center gap-2 text-sm text-gray-700">
        Price in group
        <input
          type="number"
          step="0.01"
          min="0"
          value={price}
          aria-label={`Price of ${entry.name} in this group`}
          placeholder={entry.basePrice.toFixed(2)}
          onChange={(e) => setPrice(e.target.value)}
          className={`${inputClass} w-28`}
        />
      </label>
      <label className="flex items-center gap-2 text-sm text-gray-700">
        <input type="checkbox" checked={hidden} onChange={(e) => setHidden(e.target.checked)} aria-label={`Hide ${entry.name} from this group`} />
        Hide
      </label>
      <button
        type="button"
        onClick={() => void onSave()}
        disabled={!changed || update.isPending}
        aria-label={`Save ${entry.name}`}
        className="rounded-md bg-gray-900 px-3 py-1.5 text-sm font-medium text-white disabled:opacity-50"
      >
        Save
      </button>
    </li>
  );
}
