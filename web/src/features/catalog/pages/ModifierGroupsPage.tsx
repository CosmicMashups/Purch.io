import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import type { z } from 'zod';
import { modifierGroupSchema, modifierSchema } from '../schemas';
import { useAddModifier, useAttachModifierGroupToItems, useCategories, useItems, useCreateModifierGroup, useModifierGroups, useReplaceModifierIngredients, useUpdateModifier, useUpdateModifierCategoryItem, useUpdateModifierGroup } from '../queries';
import { FormDialog, FormDialogLoader } from '../../../components/forms/FormDialog';
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
import { useState, type FormEvent } from 'react';
import { DataTable, type DataColumn } from '../../../components/lists/DataTable';
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
  const categoryGroup = (groups ?? []).find((g) => g.id === categoryGroupId) ?? null;
  const applyGroup = (groups ?? []).find((g) => g.id === applyGroupId) ?? null;
  const [view, setView] = useState<StatusView>('active');
  const categories = useCategories();
  const { run, dialog } = useLifecycle();
  // Ingredients only mean something when the business tracks its stock as ingredients.
  const tracksIngredients = useTenantSettings().data?.useSeparateInventoryTracking === true;

  const shown = (groups ?? []).filter((g) => (view === 'active' ? g.isActive !== false : g.isActive === false));

  const columns: DataColumn<ModifierGroup>[] = [
    {
      header: 'Group',
      sortValue: (g) => g.name,
      cell: (g) => (
        <div>
          <p className="flex flex-wrap items-center gap-2 font-medium text-gray-900">
            {g.name}
            {g.isActive === false && <StatusBadge label="Inactive" tone="warning" />}
          </p>
          {g.categoryId && <p className="text-xs text-gray-500">Includes all items in {categories.data?.find((c) => c.id === g.categoryId)?.name ?? 'its category'}</p>}
        </div>
      ),
    },
    {
      header: 'Choice',
      className: 'whitespace-nowrap text-gray-600',
      sortValue: (g) => `${g.allowMultipleSelection ? 'Choose several' : 'Choose one'} ${g.isRequired ? 'Required' : 'Optional'}`,
      cell: (g) => `${g.allowMultipleSelection ? 'Choose several' : 'Choose one'} · ${g.isRequired ? 'Required' : 'Optional'}`,
    },
    {
      header: 'Modifiers',
      className: 'min-w-64',
      sortValue: (g) => g.modifiers.length,
      cell: (g) =>
        g.modifiers.length > 0 ? (
          <ul className="flex flex-col divide-y divide-line" aria-label={`${g.name} modifiers`}>
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
        ) : (
          <span className="text-gray-400">None yet</span>
        ),
    },
    {
      header: '',
      className: 'text-right align-top',
      cell: (g) => (
        <div className="flex items-center justify-end gap-1">
          <button
            type="button"
            aria-label={`Add modifier to ${g.name}`}
            onClick={() => setModifierDialog({ groupId: g.id, modifier: null })}
            className="hidden h-12 items-center rounded-control bg-brand text-on-brand hover:bg-brand-strong px-4 text-base font-semibold sm:inline-flex"
          >
            Add modifier
          </button>
          <RowActionsMenu
            subject={g.name}
            actions={[
              { label: 'Edit group', onSelect: () => setGroupDialog(g) },
              { label: 'Add modifier', onSelect: () => setModifierDialog({ groupId: g.id, modifier: null }) },
              { label: 'Linked category', onSelect: () => setCategoryGroupId(g.id) },
              { label: 'Add to items', onSelect: () => setApplyGroupId(g.id) },
              g.isActive === false
                ? { label: 'Make active', onSelect: () => run({ kind: 'ModifierGroup', id: g.id, name: g.name }, 'reactivate'), separated: true }
                : { label: 'Make inactive', onSelect: () => run({ kind: 'ModifierGroup', id: g.id, name: g.name }, 'deactivate'), separated: true },
              { label: 'Delete', danger: true, onSelect: () => run({ kind: 'ModifierGroup', id: g.id, name: g.name }, 'delete') },
            ]}
          />
        </div>
      ),
    },
  ];

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
          {!isError && shown.length > 0 && <DataTable label="Modifier groups" rows={shown} columns={columns} getId={(g) => g.id} />}
        </>
      )}

      {groupDialog && <GroupDialog group={groupDialog === 'new' ? null : groupDialog} categories={categories.data ?? []} onClose={() => setGroupDialog(null)} />}
      {modifierDialog && <ModifierDialog groupId={modifierDialog.groupId} modifier={modifierDialog.modifier} onClose={() => setModifierDialog(null)} />}
      {categoryGroup && <CategoryDialog group={categoryGroup} categories={categories.data ?? []} onClose={() => setCategoryGroupId(null)} />}
      {applyGroup && <ApplyToItemsDialog group={applyGroup} categories={categories.data ?? []} onClose={() => setApplyGroupId(null)} />}
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
            ...(tracksIngredients ? [{ label: 'Ingredients', onSelect: () => setPanel('ingredients') }] : []),
            inactive
              ? { label: 'Make active', onSelect: () => onLifecycle('reactivate'), separated: true }
              : { label: 'Make inactive', onSelect: () => onLifecycle('deactivate'), separated: true },
            { label: 'Delete', danger: true, onSelect: () => onLifecycle('delete') },
          ]}
        />
      </div>
      {panel === 'ingredients' && <ModifierIngredientsDialog modifier={modifier} onClose={() => setPanel(null)} />}
    </li>
  );
}

/** The inventory a modifier takes when it is chosen, set the same way an item's recipe is. */
function ModifierIngredientsDialog({ modifier, onClose }: { modifier: Modifier; onClose: () => void }) {
  const inventory = useInventoryItems();
  const save = useReplaceModifierIngredients(modifier.id);
  const [selection, setSelection] = useState<RecipeSelection>(() =>
    selectionFromRecipe((modifier.ingredients ?? []).map((i) => ({ inventoryItemId: i.inventoryItemId, inventoryItemName: i.inventoryItemName, quantityPerOrder: i.quantityPerOrder }))),
  );
  const [lineError, setLineError] = useState<{ id: string; message: string } | null>(null);

  async function onSubmit(event: FormEvent) {
    event.preventDefault();
    const result = buildRecipeLines(selection);
    if (!result.ok) {
      setLineError({ id: result.inventoryItemId, message: result.message });
      return;
    }
    try {
      await save.mutateAsync({ lines: result.lines });
      toast.success(`${modifier.name} ingredients saved`);
      onClose();
    } catch (error) {
      toast.error(userMessage(error));
    }
  }

  return (
    <FormDialogLoader title={`${modifier.name} ingredients`} ready={Boolean(inventory.data)} failed={inventory.isError ? inventory : null} onClose={onClose}>
      <FormDialog title={`${modifier.name} ingredients`} submitLabel="Save" wide busy={save.isPending} onSubmit={(e) => void onSubmit(e)} onClose={onClose}>
        {(inventory.data ?? []).length === 0 ? (
          <p className="text-base text-ink-soft">Add ingredients in Inventory first, then choose which ones this option uses.</p>
        ) : (
          <IngredientSelector
            subject="modifier"
            ingredients={inventory.data ?? []}
            selection={selection}
            onChange={(next) => {
              setLineError(null);
              setSelection(next);
            }}
            lineError={lineError}
          />
        )}
      </FormDialog>
    </FormDialogLoader>
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
      <FormField label="Linked category (optional)" error={errors.categoryId?.message}>
        <select {...register('categoryId')} className={controlClass}>
          <option value="">None</option>
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
function ApplyToItemsDialog({ group, categories, onClose }: { group: ModifierGroup; categories: { id: string; name: string }[]; onClose: () => void }) {
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

  async function onApply(event: FormEvent) {
    event.preventDefault();
    if (!ready) return;
    try {
      const result = await attach.mutateAsync(mode === 'category' ? { categoryId } : { itemIds: [...chosen] });
      toast.success(
        result.alreadyAttached > 0
          ? `${group.name} added to ${result.attached} items; ${result.alreadyAttached} already had it`
          : `${group.name} added to ${result.attached} items`,
      );
      onClose();
    } catch (error) {
      toast.error(userMessage(error));
    }
  }

  return (
    <FormDialog title={`Add ${group.name} to items`} submitLabel="Add" busy={attach.isPending} onSubmit={(e) => void onApply(e)} onClose={onClose}>
      <fieldset className="flex flex-col gap-1 text-base">
        <legend className="mb-1 font-semibold">Add {group.name} to</legend>
        <label className="flex min-h-12 items-center gap-3">
          <input type="radio" name={`apply-${group.id}`} checked={mode === 'category'} onChange={() => setMode('category')} className="size-5 accent-brand" />
          All items of a category
        </label>
        <label className="flex min-h-12 items-center gap-3">
          <input type="radio" name={`apply-${group.id}`} checked={mode === 'items'} onChange={() => setMode('items')} className="size-5 accent-brand" />
          Selected items
        </label>
      </fieldset>

      {mode === 'category' ? (
        <FormField label="Category">
          <select value={categoryId} onChange={(e) => setCategoryId(e.target.value)} className={controlClass} aria-label={`Category to give ${group.name} to`}>
            <option value="">Choose a category</option>
            {categories.map((category) => (
              <option key={category.id} value={category.id}>
                {category.name}
              </option>
            ))}
          </select>
        </FormField>
      ) : (
        <div className="flex flex-col gap-2">
          <input value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Search items" aria-label="Search items" className={controlClass} />
          <ul className="flex max-h-56 flex-col divide-y divide-line overflow-y-auto rounded-control border border-line" aria-label={`Items to give ${group.name} to`}>
            {shown.length === 0 && <li className="px-3 py-2 text-base text-ink-soft">No items found.</li>}
            {shown.map((item) => (
              <li key={item.id}>
                <label className="flex min-h-12 items-center gap-3 px-3 text-base">
                  <input type="checkbox" checked={chosen.has(item.id)} onChange={() => toggle(item.id)} className="size-5 accent-brand" />
                  {item.name}
                </label>
              </li>
            ))}
          </ul>
        </div>
      )}

      <p className="text-sm text-ink-soft">
        {mode === 'category'
          ? categoryId === ''
            ? 'Every active item in the category gets this group. Items that already have it are skipped.'
            : `${categoryCount} active ${categoryCount === 1 ? 'item' : 'items'} in this category.`
          : `${chosen.size} selected.`}
      </p>
    </FormDialog>
  );
}

/** Links the group to a category (or unlinks it) and sets, for each of that category's items, its added price or whether it is hidden. */
function CategoryDialog({ group, categories, onClose }: { group: ModifierGroup; categories: { id: string; name: string }[]; onClose: () => void }) {
  const updateGroup = useUpdateModifierGroup(group.id);
  const updateItem = useUpdateModifierCategoryItem(group.id);
  const [categoryId, setCategoryId] = useState(group.categoryId ?? '');
  // Per item: the price typed (empty keeps the item's own price) and whether it is hidden.
  const [edits, setEdits] = useState<Record<string, { price: string; hidden: boolean }>>({});

  const categoryChanged = categoryId !== (group.categoryId ?? '');
  const entries = group.categoryItems ?? [];
  const current = (entry: ModifierCategoryItem) => edits[entry.itemId] ?? { price: entry.priceOverride === null ? '' : String(entry.priceOverride), hidden: entry.isExcluded };
  const changedEntries = entries.filter((entry) => {
    const edit = current(entry);
    return edit.price !== (entry.priceOverride === null ? '' : String(entry.priceOverride)) || edit.hidden !== entry.isExcluded;
  });

  function change(entry: ModifierCategoryItem, patch: Partial<{ price: string; hidden: boolean }>) {
    setEdits((all) => ({ ...all, [entry.itemId]: { ...current(entry), ...patch } }));
  }

  async function onSubmit(event: FormEvent) {
    event.preventDefault();
    try {
      if (categoryChanged) {
        await updateGroup.mutateAsync({
          name: group.name,
          allowMultipleSelection: group.allowMultipleSelection,
          isRequired: group.isRequired,
          categoryId: categoryId === '' ? null : categoryId,
        });
      } else {
        for (const entry of changedEntries) {
          const edit = current(entry);
          await updateItem.mutateAsync({ itemId: entry.itemId, body: { priceOverride: edit.price.trim() === '' ? null : Number(edit.price), isExcluded: edit.hidden } });
        }
      }
      toast.success('Saved');
      onClose();
    } catch (error) {
      toast.error(userMessage(error));
    }
  }

  return (
    <FormDialog
      title={`Linked category of ${group.name}`}
      submitLabel="Save"
      wide
      busy={updateGroup.isPending || updateItem.isPending}
      onSubmit={(e) => void onSubmit(e)}
      onClose={onClose}
    >
      <FormField
        label="Linked category"
        hint="Its items are offered as choices. Changing the category resets the added prices."
      >
        <select value={categoryId} onChange={(e) => setCategoryId(e.target.value)} className={controlClass}>
          <option value="">None</option>
          {categories.map((category) => (
            <option key={category.id} value={category.id}>
              {category.name}
            </option>
          ))}
        </select>
      </FormField>

      {categoryChanged ? (
        categoryId !== '' && <p className="text-sm text-ink-soft">Save to set an added price for each item of this category.</p>
      ) : (
        group.categoryId && (
          <div className="flex flex-col gap-2">
            <p className="text-sm text-ink-soft">Items are added at their own price. Set an added price to charge something else in this group, or hide an item.</p>
            {entries.length === 0 && <p className="text-base text-ink-soft">No active items in this category.</p>}
            <ul className="flex flex-col divide-y divide-line" aria-label={`${group.name} category items`}>
              {entries.map((entry) => {
                const edit = current(entry);
                return (
                  <li key={entry.itemId} className="flex flex-wrap items-center gap-3 py-2">
                    <span className="min-w-40 flex-1 text-base font-semibold">
                      {entry.name} <span className="font-normal text-ink-soft">(₱{entry.basePrice.toFixed(2)})</span>
                    </span>
                    <label className="flex items-center gap-2 text-base">
                      Added price
                      <input
                        type="number"
                        step="0.01"
                        min="0"
                        inputMode="decimal"
                        value={edit.price}
                        aria-label={`Added price for ${entry.name}`}
                        placeholder={entry.basePrice.toFixed(2)}
                        onChange={(e) => change(entry, { price: e.target.value })}
                        className={`${controlClass} w-32`}
                      />
                    </label>
                    <label className="flex min-h-12 items-center gap-2 text-base">
                      <input
                        type="checkbox"
                        checked={edit.hidden}
                        aria-label={`Hide ${entry.name}`}
                        onChange={(e) => change(entry, { hidden: e.target.checked })}
                        className="size-5 accent-brand"
                      />
                      Hide
                    </label>
                  </li>
                );
              })}
            </ul>
          </div>
        )
      )}
    </FormDialog>
  );
}
