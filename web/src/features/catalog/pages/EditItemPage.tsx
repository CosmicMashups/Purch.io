import { useForm, useWatch } from 'react-hook-form';
import { ITEM_SAMPLES } from '../../../lib/images';
import { zodResolver } from '@hookform/resolvers/zod';
import { useNavigate, useParams } from 'react-router-dom';
import type { z } from 'zod';
import { updateItemSchema } from '../schemas';
import { useCategories, useItems, useRecipe, useReplaceRecipe, useUpdateItem } from '../queries';
import { useTenantSettings } from '../../tenant/queries';
import { useInventoryItems } from '../../inventory/queries';
import { IngredientSelector } from '../components/IngredientSelector';
import { buildRecipeLines, selectionFromRecipe, type RecipeSelection } from '../recipe';
import { Field, inputClass } from '../../../components/Field';
import { ImageUploadField } from '../../../components/forms/ImageUploadField';
import { StatusBadge } from '../../../components/StatusBadge';
import { pricingTypeLabels } from '../labels';
import { ApiError } from '../../../lib/apiError';
import { useEffect, useState } from 'react';

type FormValues = z.infer<typeof updateItemSchema>;

/** Key order must not matter when comparing two selections. */
const sorted = (selection: RecipeSelection) => Object.entries(selection).sort(([a], [b]) => a.localeCompare(b));

export function EditItemPage() {
  const { itemId } = useParams<{ itemId: string }>();
  const navigate = useNavigate();
  const { data: items } = useItems();
  const { data: categories } = useCategories();
  const updateItem = useUpdateItem();
  const [submitError, setSubmitError] = useState<string | null>(null);

  const tracksIngredients = useTenantSettings().data?.useSeparateInventoryTracking === true;
  const inventoryItems = useInventoryItems();
  const savedRecipe = useRecipe(tracksIngredients ? (itemId ?? '') : '');
  const replaceRecipe = useReplaceRecipe(itemId ?? '');
  const [recipe, setRecipe] = useState<RecipeSelection>({});
  const [recipeError, setRecipeError] = useState<{ id: string; message: string } | null>(null);
  // What the form started from, so saving only touches the recipe when it was really changed.
  const [initialRecipe, setInitialRecipe] = useState<RecipeSelection | null>(null);

  useEffect(() => {
    if (!savedRecipe.data || initialRecipe) return;
    const fromServer = selectionFromRecipe(savedRecipe.data);
    setInitialRecipe(fromServer);
    setRecipe(fromServer);
  }, [savedRecipe.data, initialRecipe]);

  // An item's own paired stock record cannot be an ingredient of its own recipe. A retired one stays pickable only if already chosen.
  const pickable = (inventoryItems.data ?? []).filter((i) => i.linkedItemId !== itemId && (i.isActive || (initialRecipe !== null && i.id in initialRecipe)));

  const item = items?.find((i) => i.id === itemId);

  const {
    register,
    handleSubmit,
    control,
    setValue,
    formState: { errors },
  } = useForm<FormValues>({
    resolver: zodResolver(updateItemSchema),
    values: item
      ? {
          name: item.name,
          sku: item.sku,
          barcode: item.barcode,
          categoryId: item.categoryId,
          basePrice: item.basePrice,
          imageUrl: item.imageUrl,
          isActive: item.isActive,
          departmentId: item.departmentId,
        }
      : undefined,
  });

  const imageUrl = useWatch({ control, name: 'imageUrl' });

  if (!item || !itemId) {
    return <p className="text-sm text-gray-500">Loading…</p>;
  }

  async function onSubmit(values: FormValues) {
    setSubmitError(null);
    setRecipeError(null);
    const recipeChanged = tracksIngredients && initialRecipe !== null && JSON.stringify(sorted(recipe)) !== JSON.stringify(sorted(initialRecipe));
    const recipeLines = recipeChanged ? buildRecipeLines(recipe) : { ok: true as const, lines: [] };
    if (!recipeLines.ok) {
      setRecipeError({ id: recipeLines.inventoryItemId, message: recipeLines.message });
      return;
    }
    try {
      await updateItem.mutateAsync({
        itemId: itemId!,
        body: {
          name: values.name,
          sku: values.sku ?? null,
          barcode: values.barcode ?? null,
          categoryId: values.categoryId ?? null,
          basePrice: values.basePrice,
          imageUrl: values.imageUrl ?? null,
          isActive: values.isActive,
          departmentId: values.departmentId ?? null,
        },
      });
      // An empty list is a real change here: it turns the item back into its own inventory item.
      if (recipeChanged) await replaceRecipe.mutateAsync({ lines: recipeLines.lines });
      navigate('/catalog/items');
    } catch (err) {
      setSubmitError(err instanceof ApiError ? err.message : 'Failed to update item');
    }
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="flex max-w-xl flex-col gap-4">
      <div className="flex items-center gap-2">
        <h1 className="text-xl font-semibold text-gray-900">Edit Item</h1>
        <StatusBadge label={pricingTypeLabels[item.pricingType]} />
      </div>
      <p className="text-xs text-gray-500">Pricing type is fixed at creation and cannot be changed.</p>

      <Field label="Name" error={errors.name?.message}>
        <input {...register('name')} className={inputClass} />
      </Field>

      <Field label="SKU">
        <input {...register('sku')} className={inputClass} />
      </Field>

      <Field label="Barcode">
        <input {...register('barcode')} className={inputClass} />
      </Field>

      <Field label="Category">
        <select {...register('categoryId')} className={inputClass}>
          <option value="">None (Uncategorized)</option>
          {(categories ?? []).map((c) => (
            <option key={c.id} value={c.id}>
              {c.name}
            </option>
          ))}
        </select>
      </Field>

      <Field label="Base Price" error={errors.basePrice?.message}>
        <input type="number" step="0.01" {...register('basePrice')} className={inputClass} />
      </Field>

      <input type="hidden" {...register('imageUrl')} />
      <ImageUploadField label="Image" allowUrl samples={ITEM_SAMPLES} value={imageUrl ?? null} onChange={(url) => setValue('imageUrl', url, { shouldDirty: true })} />

      <label className="flex items-center gap-2 text-sm font-medium text-gray-700">
        <input type="checkbox" {...register('isActive')} />
        Active
      </label>

      {tracksIngredients &&
        (initialRecipe === null ? (
          <p className="text-sm text-gray-500">Loading ingredients…</p>
        ) : (
          <IngredientSelector ingredients={pickable} selection={recipe} onChange={setRecipe} lineError={recipeError} />
        ))}

      {submitError && <p className="text-sm text-red-600">{submitError}</p>}

      <button
        type="submit"
        disabled={updateItem.isPending || replaceRecipe.isPending}
        className="self-start rounded-md bg-gray-900 px-4 py-2 text-sm font-medium text-white disabled:opacity-50"
      >
        {updateItem.isPending || replaceRecipe.isPending ? 'Saving…' : 'Save Changes'}
      </button>
    </form>
  );
}
