import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { ErrorState } from '../../../components/ErrorState';
import { Skeleton } from '../../../components/Skeleton';
import { toast } from '../../../components/feedback/toastStore';
import { FormField, PrimaryButton, controlClass } from '../../../components/forms/FormField';
import { userMessage } from '../../../lib/apiError';
import { useInventoryItems } from '../../inventory/queries';
import type { InventoryItem } from '../../inventory/types';
import { useItems, useRecipe, useReplaceRecipe } from '../queries';
import { buildRecipeLines, selectionFromRecipe, type RecipeSelection } from '../recipe';
import type { ItemRecipeLine } from '../types';
import { ItemSubPageHeader } from './ItemSubPageHeader';

export function RecipePage() {
  const { itemId = '' } = useParams<{ itemId: string }>();
  const items = useItems();
  const inventoryItems = useInventoryItems();
  const recipe = useRecipe(itemId);

  const failed = inventoryItems.isError ? inventoryItems : recipe.isError ? recipe : null;
  // An item's own paired stock record cannot be an ingredient of its own recipe.
  const ingredients = inventoryItems.data?.filter((i) => i.linkedItemId !== itemId);

  return (
    <div>
      <ItemSubPageHeader itemId={itemId} title="Recipe" />
      {failed ? (
        <ErrorState title="The recipe could not be loaded" message={userMessage(failed.error)} onRetry={() => void failed.refetch()} />
      ) : !ingredients || !recipe.data || !items.data ? (
        <div className="flex max-w-2xl flex-col gap-3" aria-busy="true">
          <Skeleton className="h-16 w-full" />
          <Skeleton className="h-16 w-full" />
        </div>
      ) : (
        <RecipeEditor
          key={itemId}
          itemId={itemId}
          itemName={items.data.find((i) => i.id === itemId)?.name ?? 'this item'}
          ingredients={ingredients}
          lines={recipe.data}
        />
      )}
    </div>
  );
}

function RecipeEditor({ itemId, itemName, ingredients, lines }: { itemId: string; itemName: string; ingredients: InventoryItem[]; lines: ItemRecipeLine[] }) {
  const navigate = useNavigate();
  const save = useReplaceRecipe(itemId);
  const [selection, setSelection] = useState<RecipeSelection>(() => selectionFromRecipe(lines));
  const [lineError, setLineError] = useState<{ id: string; message: string } | null>(null);

  if (ingredients.length === 0) {
    return (
      <p className="max-w-2xl rounded-panel border border-dashed border-ink-soft/40 p-6 text-base text-ink-soft">
        No inventory items yet. Add some in Inventory before building a recipe.
      </p>
    );
  }

  function toggle(id: string, checked: boolean) {
    setLineError(null);
    setSelection((current) => {
      const next = { ...current };
      if (checked) next[id] = '';
      else delete next[id];
      return next;
    });
  }

  async function onSave() {
    const result = buildRecipeLines(selection);
    if (!result.ok) {
      setLineError({ id: result.inventoryItemId, message: result.message });
      return;
    }
    setLineError(null);
    await save.mutateAsync({ lines: result.lines });
    toast.success(`Recipe for ${itemName} saved`);
    navigate('/catalog/items');
  }

  return (
    <div className="flex max-w-2xl flex-col gap-4">
      <p className="text-base text-ink-soft">
        An item is either its own inventory item or made from a recipe, never both. Saving a recipe retires this item's own stock record
        (its stock must be zero first). Save an empty recipe to make it an inventory item again.
      </p>

      <ul className="flex flex-col gap-3">
        {ingredients.map((ingredient) => {
          const checked = ingredient.id in selection;
          return (
            <li key={ingredient.id} className="rounded-panel border border-line bg-surface p-4">
              <label className="flex min-h-12 items-center gap-3 text-base font-semibold">
                <input
                  type="checkbox"
                  checked={checked}
                  onChange={(e) => toggle(ingredient.id, e.target.checked)}
                  className="size-6 accent-brand"
                />
                <span>
                  {ingredient.name}
                  <span className="block text-sm font-normal text-ink-soft">
                    {ingredient.baseUnit} / {ingredient.packagingUnit}
                  </span>
                </span>
              </label>
              {checked && (
                <div className="mt-3 sm:pl-9">
                  <FormField
                    label={`Quantity per order (${ingredient.baseUnit})`}
                    hint="Leave blank to just check availability"
                    error={lineError?.id === ingredient.id ? lineError.message : undefined}
                  >
                    <input
                      inputMode="decimal"
                      value={selection[ingredient.id]}
                      onChange={(e) => setSelection((current) => ({ ...current, [ingredient.id]: e.target.value }))}
                      className={controlClass}
                    />
                  </FormField>
                </div>
              )}
            </li>
          );
        })}
      </ul>

      <div>
        <PrimaryButton type="button" busy={save.isPending} onClick={() => void onSave()}>
          {save.isPending ? 'Saving...' : 'Save recipe'}
        </PrimaryButton>
      </div>
    </div>
  );
}
