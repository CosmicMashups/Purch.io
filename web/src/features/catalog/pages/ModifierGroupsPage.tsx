import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import type { z } from 'zod';
import { modifierGroupSchema, modifierSchema } from '../schemas';
import { useAddModifier, useCategories, useCreateModifierGroup, useModifierGroups, useReplaceModifierIngredients, useUpdateModifier, useUpdateModifierCategoryItem, useUpdateModifierGroup } from '../queries';
import { Field, inputClass } from '../../../components/Field';
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

type GroupFormValues = z.infer<typeof modifierGroupSchema>;
type ModifierFormValues = z.infer<typeof modifierSchema>;

export function ModifierGroupsPage() {
  const { data: groups, isLoading, isError, error, refetch } = useModifierGroups();
  const createModifierGroup = useCreateModifierGroup();
  const [activeGroupId, setActiveGroupId] = useState<string | null>(null);
  const [categoryGroupId, setCategoryGroupId] = useState<string | null>(null);
  const categories = useCategories();
  // Ingredients only mean something when the business tracks its stock as ingredients.
  const tracksIngredients = useTenantSettings().data?.useSeparateInventoryTracking === true;

  const {
    register: registerGroup,
    handleSubmit: handleGroupSubmit,
    reset: resetGroup,
    formState: { errors: groupErrors },
  } = useForm<GroupFormValues>({
    resolver: zodResolver(modifierGroupSchema),
    defaultValues: { name: '', allowMultipleSelection: false, isRequired: false, categoryId: null },
  });

  async function onCreateGroup(values: GroupFormValues) {
    await createModifierGroup.mutateAsync(values);
    resetGroup({ name: '', allowMultipleSelection: false, isRequired: false, categoryId: null });
  }

  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-xl font-semibold text-gray-900">Modifier Groups</h1>

      <form onSubmit={handleGroupSubmit(onCreateGroup)} className="flex max-w-md flex-col gap-3">
        <Field label="Group name" error={groupErrors.name?.message}>
          <input {...registerGroup('name')} className={inputClass} placeholder="Spice Level" />
        </Field>
        <label className="flex items-center gap-2 text-sm font-medium text-gray-700">
          <input type="checkbox" {...registerGroup('allowMultipleSelection')} />
          Allow multiple selection
        </label>
        <label className="flex items-center gap-2 text-sm font-medium text-gray-700">
          <input type="checkbox" {...registerGroup('isRequired')} />
          Required
        </label>
        <Field label="Offer items from a category (optional)" error={groupErrors.categoryId?.message}>
          <select {...registerGroup('categoryId')} className={inputClass}>
            <option value="">None, only the modifiers below</option>
            {(categories.data ?? []).map((category) => (
              <option key={category.id} value={category.id}>
                {category.name}
              </option>
            ))}
          </select>
        </Field>
        <button
          type="submit"
          disabled={createModifierGroup.isPending}
          className="self-start rounded-md bg-gray-900 px-4 py-2 text-sm font-medium text-white disabled:opacity-50"
        >
          {createModifierGroup.isPending ? 'Saving…' : 'Add Group'}
        </button>
      </form>

      {isLoading && <SkeletonList />}
      {isError && <ErrorState message={describeQueryError(error)} onRetry={() => refetch()} />}
      {!isLoading && !isError && (groups ?? []).length === 0 && <EmptyState title="No modifier groups yet" />}
      <ul className="flex flex-col gap-3">
        {!isError && (groups ?? []).map((g) => (
          <li key={g.id} className="rounded-lg border border-gray-200 bg-white p-4">
            <div className="flex items-center justify-between">
              <div>
                <p className="font-medium text-gray-900">{g.name}</p>
                <p className="text-xs text-gray-500">
                  {g.allowMultipleSelection ? 'Multiple selection' : 'Single selection'} ·{' '}
                  {g.isRequired ? 'Required' : 'Optional'}
                </p>
              </div>
              <div className="flex gap-3">
                <button
                  type="button"
                  onClick={() => setCategoryGroupId(categoryGroupId === g.id ? null : g.id)}
                  className="text-sm text-gray-500 hover:text-gray-900 hover:underline"
                >
                  {categoryGroupId === g.id ? 'Close category' : `Category of ${g.name}`}
                </button>
                <button
                  onClick={() => setActiveGroupId(activeGroupId === g.id ? null : g.id)}
                  className="text-sm text-gray-500 hover:text-gray-900 hover:underline"
                >
                  {activeGroupId === g.id ? 'Close' : 'Add Modifier'}
                </button>
              </div>
            </div>

            {g.categoryId && (
              <p className="mt-1 text-xs text-gray-500">
                Also offers every item in {categories.data?.find((c) => c.id === g.categoryId)?.name ?? 'its category'}
              </p>
            )}
            {categoryGroupId === g.id && <CategoryPanel group={g} categories={categories.data ?? []} />}

            {g.modifiers.length > 0 && (
              <ul className="mt-2 flex flex-col divide-y divide-gray-100" aria-label={`${g.name} modifiers`}>
                {g.modifiers.map((m) => (
                  <ModifierRow key={m.id} modifier={m} tracksIngredients={tracksIngredients} />
                ))}
              </ul>
            )}

            {activeGroupId === g.id && <AddModifierForm groupId={g.id} />}
          </li>
        ))}
      </ul>
    </div>
  );
}

/** One modifier: its price and, when stock is tracked, what it uses up. Both can be changed in place. */
function ModifierRow({ modifier, tracksIngredients }: { modifier: Modifier; tracksIngredients: boolean }) {
  const [panel, setPanel] = useState<'edit' | 'ingredients' | null>(null);
  const ingredientNames = (modifier.ingredients ?? []).map((i) => i.inventoryItemName);

  return (
    <li className="py-2">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div>
          <p className="text-sm font-medium text-gray-900">
            {modifier.name} <span className="font-normal text-gray-600">({modifier.priceDelta < 0 ? '-' : '+'}₱{Math.abs(modifier.priceDelta).toFixed(2)})</span>
            {modifier.isOutOfStock && <span className="ml-2 rounded bg-red-100 px-1.5 py-0.5 text-xs font-medium text-red-800">Sold out</span>}
          </p>
          {tracksIngredients && <p className="text-xs text-gray-500">{ingredientNames.length > 0 ? `Uses ${ingredientNames.join(', ')}` : 'Uses no ingredients'}</p>}
        </div>
        <div className="flex gap-3 text-sm">
          <button type="button" onClick={() => setPanel(panel === 'edit' ? null : 'edit')} className="text-gray-600 underline hover:text-gray-900">
            {panel === 'edit' ? 'Close' : `Edit ${modifier.name}`}
          </button>
          {tracksIngredients && (
            <button type="button" onClick={() => setPanel(panel === 'ingredients' ? null : 'ingredients')} className="text-gray-600 underline hover:text-gray-900">
              {panel === 'ingredients' ? 'Close ingredients' : `Ingredients of ${modifier.name}`}
            </button>
          )}
        </div>
      </div>
      {panel === 'edit' && <EditModifierForm modifier={modifier} onDone={() => setPanel(null)} />}
      {panel === 'ingredients' && <ModifierIngredientsEditor modifier={modifier} onDone={() => setPanel(null)} />}
    </li>
  );
}

function EditModifierForm({ modifier, onDone }: { modifier: Modifier; onDone: () => void }) {
  const update = useUpdateModifier();
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<ModifierFormValues>({ resolver: zodResolver(modifierSchema), defaultValues: { name: modifier.name, priceDelta: modifier.priceDelta } });

  async function onSubmit(values: ModifierFormValues) {
    try {
      await update.mutateAsync({ modifierId: modifier.id, body: values });
      onDone();
    } catch (error) {
      toast.error(userMessage(error));
    }
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} aria-label={`Edit ${modifier.name}`} className="mt-2 flex max-w-sm flex-col gap-2 rounded-md bg-gray-50 p-3">
      <Field label="Modifier name" error={errors.name?.message}>
        <input {...register('name')} className={inputClass} />
      </Field>
      <Field label="Price delta" error={errors.priceDelta?.message}>
        <input type="number" step="0.01" {...register('priceDelta')} className={inputClass} />
      </Field>
      <button type="submit" disabled={update.isPending} className="self-start rounded-md bg-gray-900 px-3 py-1.5 text-sm font-medium text-white disabled:opacity-50">
        {update.isPending ? 'Saving…' : 'Save changes'}
      </button>
    </form>
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

function AddModifierForm({ groupId }: { groupId: string }) {
  const addModifier = useAddModifier(groupId);
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<ModifierFormValues>({ resolver: zodResolver(modifierSchema), defaultValues: { priceDelta: 0 } });

  async function onSubmit(values: ModifierFormValues) {
    await addModifier.mutateAsync(values);
    reset({ name: '', priceDelta: 0 });
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="mt-3 flex max-w-sm flex-col gap-2 border-t border-gray-100 pt-3">
      <Field label="Modifier name" error={errors.name?.message}>
        <input {...register('name')} className={inputClass} />
      </Field>
      <Field label="Price delta" error={errors.priceDelta?.message}>
        <input type="number" step="0.01" {...register('priceDelta')} className={inputClass} />
      </Field>
      <button
        type="submit"
        disabled={addModifier.isPending}
        className="self-start rounded-md bg-gray-900 px-3 py-1.5 text-sm font-medium text-white disabled:opacity-50"
      >
        {addModifier.isPending ? 'Saving…' : 'Add'}
      </button>
    </form>
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
