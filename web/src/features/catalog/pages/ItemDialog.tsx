import { useEffect, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { useForm, useWatch } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import type { z } from 'zod';
import { ITEM_SAMPLES } from '../../../lib/images';
import { createItemSchema, updateItemSchema } from '../schemas';
import { PricingType, TingiMode, type Item } from '../types';
import { pricingTypeLabels } from '../labels';
import { catalogApi } from '../api';
import { catalogKeys, useCategories, useCreateItem, useRecipe, useReplaceRecipe, useUpdateItem } from '../queries';
import { useTenantSettings } from '../../tenant/queries';
import { useInventoryItems } from '../../inventory/queries';
import { IngredientSelector } from '../components/IngredientSelector';
import { EquipmentSelector } from '../components/EquipmentSelector';
import { equipmentApi } from '../../equipment/api';
import { useEquipment, useItemEquipment, useReplaceItemEquipment } from '../../equipment/queries';
import { buildRecipeLines, selectionFromRecipe, type RecipeSelection } from '../recipe';
import { Field, inputClass } from '../../../components/Field';
import { ImageUploadField } from '../../../components/forms/ImageUploadField';
import { Modal } from '../../../components/Modal';
import { StatusBadge } from '../../../components/StatusBadge';
import { ApiError, userMessage } from '../../../lib/apiError';
import { toast } from '../../../components/feedback/toastStore';

type CreateValues = z.infer<typeof createItemSchema>;
type EditValues = z.infer<typeof updateItemSchema>;

const FORM_ID = 'item-dialog-form';

/** Key order must not matter when comparing two selections. */
const sorted = (selection: RecipeSelection) => Object.entries(selection).sort(([a], [b]) => a.localeCompare(b));

/** The low-stock alert is a plain field: blank clears it, anything else must be a whole number from zero up. */
function parseThreshold(text: string): { ok: true; value: number | null } | { ok: false; message: string } {
  if (text.trim() === '') return { ok: true, value: null };
  const value = Number(text);
  if (!Number.isInteger(value) || value < 0) return { ok: false, message: 'Enter a whole number, zero or more' };
  return { ok: true, value };
}

/** Add Item (no `item`) or Edit Item (with one), as a dialog over the Items list. */
export function ItemDialog({ item, onClose }: { item: Item | null; onClose: () => void }) {
  const [busy, setBusy] = useState(false);
  return (
    <Modal
      open
      wide
      title={item ? 'Edit Item' : 'Add Item'}
      onClose={onClose}
      footer={
        <div className="flex justify-end gap-2">
          <button type="button" onClick={onClose} className="rounded-md border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700">
            Cancel
          </button>
          <button type="submit" form={FORM_ID} disabled={busy} className="rounded-md bg-gray-900 px-4 py-2 text-sm font-medium text-white disabled:opacity-50">
            {busy ? 'Saving…' : item ? 'Save Changes' : 'Save Item'}
          </button>
        </div>
      }
    >
      {item ? <EditForm item={item} onClose={onClose} onBusy={setBusy} /> : <AddForm onClose={onClose} onBusy={setBusy} />}
    </Modal>
  );
}

function ThresholdField({ value, onChange, error }: { value: string; onChange: (v: string) => void; error: string | null }) {
  return (
    <Field label="Low-stock alert (blank for none)" error={error ?? undefined}>
      <input type="number" min={0} value={value} onChange={(e) => onChange(e.target.value)} className={inputClass} />
    </Field>
  );
}

function AddForm({ onClose, onBusy }: { onClose: () => void; onBusy: (busy: boolean) => void }) {
  const { data: categories } = useCategories();
  const createItem = useCreateItem();
  const qc = useQueryClient();
  // Only a business that tracks ingredients separately has recipes at all.
  const tracksIngredients = useTenantSettings().data?.useSeparateInventoryTracking === true;
  const inventoryItems = useInventoryItems();
  const pickable = (inventoryItems.data ?? []).filter((i) => i.isActive);
  const [recipe, setRecipe] = useState<RecipeSelection>({});
  const [recipeError, setRecipeError] = useState<{ id: string; message: string } | null>(null);
  const equipment = useEquipment();
  const pickableEquipment = (equipment.data ?? []).filter((e) => e.isActive);
  const [equipmentIds, setEquipmentIds] = useState<string[]>([]);
  const [threshold, setThreshold] = useState('');
  const [thresholdError, setThresholdError] = useState<string | null>(null);

  useEffect(() => onBusy(createItem.isPending), [createItem.isPending, onBusy]);

  const {
    register,
    handleSubmit,
    watch,
    setValue,
    formState: { errors },
  } = useForm<CreateValues>({
    resolver: zodResolver(createItemSchema),
    defaultValues: { pricingType: PricingType.Unit, sortOrder: 0 },
  });

  const pricingType = Number(watch('pricingType'));
  const imageUrl = watch('imageUrl');

  // Sub-module fields — best-effort, mirroring add_item_screen.dart: if these don't parse to
  // a valid value, the item is still created and the matching secondary call is silently skipped.
  const [packSize, setPackSize] = useState('');
  const [serviceDuration, setServiceDuration] = useState('');
  const [bundleTriggerQty, setBundleTriggerQty] = useState('');
  const [bundlePrice, setBundlePrice] = useState('');
  const [variantAttrKey, setVariantAttrKey] = useState('');
  const [variantAttrValue, setVariantAttrValue] = useState('');
  const [comboSlotLabel, setComboSlotLabel] = useState('');
  const [comboCategoryId, setComboCategoryId] = useState('');
  const [comboQuantity, setComboQuantity] = useState('1');
  const [comboUpcharge, setComboUpcharge] = useState('0');
  const [submitError, setSubmitError] = useState<string | null>(null);

  async function createSubResourceIfValid(itemId: string, values: CreateValues) {
    if (values.pricingType === PricingType.WeightVolume) {
      const size = Number(packSize);
      if (Number.isFinite(size) && size > 0) {
        await catalogApi.updateTingiConfig(itemId, {
          tingiMode: TingiMode.Fixed,
          packagedSize: size,
          tingiIncrementStep: null,
          allowedSizes: [],
        });
      }
    }

    if (values.pricingType === PricingType.Service) {
      const duration = Number.parseInt(serviceDuration, 10);
      if (Number.isFinite(duration) && duration > 0) {
        await catalogApi.updateServiceDuration(itemId, { durationMinutes: duration });
      }
    }

    if (values.pricingType === PricingType.Bundle) {
      const triggerQty = Number.parseInt(bundleTriggerQty, 10);
      const price = Number(bundlePrice);
      if (Number.isFinite(triggerQty) && triggerQty > 0 && Number.isFinite(price)) {
        await catalogApi.createBundleRule(itemId, {
          description: `Buy ${triggerQty} for ₱${price}`,
          triggerQuantity: triggerQty,
          bundlePrice: price,
        });
      }
    }

    if (values.pricingType === PricingType.VariantMatrix) {
      if (variantAttrKey.trim() && variantAttrValue.trim()) {
        await catalogApi.createVariant(itemId, {
          attributes: { [variantAttrKey.trim()]: variantAttrValue.trim() },
          sku: null,
          priceOverride: null,
          imageUrl: null,
        });
      }
    }

    if (values.pricingType === PricingType.Combo) {
      if (comboSlotLabel.trim() && comboCategoryId) {
        await catalogApi.createComboComponent(itemId, {
          componentCategoryId: comboCategoryId,
          slotLabel: comboSlotLabel.trim(),
          quantity: Number.parseInt(comboQuantity, 10) || 1,
          substitutionUpchargeAmount: Number(comboUpcharge) || 0,
        });
      }
    }
  }

  async function onSubmit(values: CreateValues) {
    setSubmitError(null);
    setRecipeError(null);
    setThresholdError(null);
    const parsedThreshold = parseThreshold(threshold);
    if (!parsedThreshold.ok) {
      setThresholdError(parsedThreshold.message);
      return;
    }
    // Checked up front so a bad quantity never leaves a half-made item behind.
    const recipeLines = tracksIngredients ? buildRecipeLines(recipe) : { ok: true as const, lines: [] };
    if (!recipeLines.ok) {
      setRecipeError({ id: recipeLines.inventoryItemId, message: recipeLines.message });
      return;
    }
    try {
      const item = await createItem.mutateAsync({
        name: values.name,
        sku: values.sku ?? null,
        barcode: values.barcode ?? null,
        categoryId: values.categoryId ?? null,
        basePrice: values.basePrice,
        imageUrl: values.imageUrl ?? null,
        pricingType: values.pricingType,
        sortOrder: values.sortOrder,
      });

      await createSubResourceIfValid(item.id, values);
      if (parsedThreshold.value !== null) await catalogApi.updateLowStockThreshold(item.id, { threshold: parsedThreshold.value });

      if (recipeLines.lines.length > 0) {
        try {
          await catalogApi.replaceRecipe(item.id, { lines: recipeLines.lines });
        } catch (recipeFailure) {
          // The item exists now, so resubmitting would make a duplicate. Close, and let them fix just the ingredients.
          toast.error(`${values.name} was saved, but its ingredients were not: ${userMessage(recipeFailure)} Open Edit on it to add them again.`);
          await qc.invalidateQueries({ queryKey: catalogKeys.items });
          onClose();
          return;
        }
        await qc.invalidateQueries({ queryKey: ['inventory-items'] });
      }
      if (equipmentIds.length > 0) {
        try {
          await equipmentApi.replaceItemEquipment(item.id, equipmentIds);
        } catch (equipmentFailure) {
          // The item exists now, so resubmitting would make a duplicate. Close, and let them fix just the equipment.
          toast.error(`${values.name} was saved, but its equipment was not: ${userMessage(equipmentFailure)} Open Edit on it to add it again.`);
          await qc.invalidateQueries({ queryKey: catalogKeys.items });
          onClose();
          return;
        }
        await qc.invalidateQueries({ queryKey: ['equipment'] });
      }
      await qc.invalidateQueries({ queryKey: catalogKeys.items });

      onClose();
    } catch (err) {
      setSubmitError(err instanceof ApiError ? err.message : 'Failed to create item');
    }
  }

  return (
    <form id={FORM_ID} onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-4">
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

      <Field label="Sort order" error={errors.sortOrder?.message}>
        <input type="number" {...register('sortOrder')} className={inputClass} />
      </Field>

      <ThresholdField value={threshold} onChange={setThreshold} error={thresholdError} />

      <input type="hidden" {...register('imageUrl')} />
      <ImageUploadField label="Image" allowUrl samples={ITEM_SAMPLES} value={imageUrl ?? null} onChange={(url) => setValue('imageUrl', url, { shouldDirty: true })} />

      <Field label="Pricing Type">
        <select {...register('pricingType', { valueAsNumber: true })} className={inputClass}>
          {Object.entries(pricingTypeLabels).map(([value, label]) => (
            <option key={value} value={value}>
              {label}
            </option>
          ))}
        </select>
      </Field>

      {pricingType === PricingType.WeightVolume && (
        <Field label="Pack size (e.g. kg / liter)">
          <input value={packSize} onChange={(e) => setPackSize(e.target.value)} className={inputClass} />
        </Field>
      )}

      {pricingType === PricingType.Service && (
        <Field label="Service duration (minutes)">
          <input value={serviceDuration} onChange={(e) => setServiceDuration(e.target.value)} className={inputClass} />
        </Field>
      )}

      {pricingType === PricingType.Bundle && (
        <>
          <Field label="Trigger quantity">
            <input value={bundleTriggerQty} onChange={(e) => setBundleTriggerQty(e.target.value)} className={inputClass} />
          </Field>
          <Field label="Bundle price">
            <input value={bundlePrice} onChange={(e) => setBundlePrice(e.target.value)} className={inputClass} />
          </Field>
        </>
      )}

      {pricingType === PricingType.VariantMatrix && (
        <>
          <Field label="Attribute name (e.g. Size)">
            <input value={variantAttrKey} onChange={(e) => setVariantAttrKey(e.target.value)} className={inputClass} />
          </Field>
          <Field label="Attribute value (e.g. Large)">
            <input value={variantAttrValue} onChange={(e) => setVariantAttrValue(e.target.value)} className={inputClass} />
          </Field>
        </>
      )}

      {pricingType === PricingType.Combo && (
        <>
          <Field label="Slot label">
            <input value={comboSlotLabel} onChange={(e) => setComboSlotLabel(e.target.value)} className={inputClass} />
          </Field>
          <Field label="Component category">
            <select value={comboCategoryId} onChange={(e) => setComboCategoryId(e.target.value)} className={inputClass}>
              <option value="">Select a category</option>
              {(categories ?? []).map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </select>
          </Field>
          <Field label="Quantity">
            <input value={comboQuantity} onChange={(e) => setComboQuantity(e.target.value)} className={inputClass} />
          </Field>
          <Field label="Substitution upcharge">
            <input value={comboUpcharge} onChange={(e) => setComboUpcharge(e.target.value)} className={inputClass} />
          </Field>
        </>
      )}

      {tracksIngredients && <IngredientSelector ingredients={pickable} selection={recipe} onChange={setRecipe} lineError={recipeError} />}

      <EquipmentSelector equipment={pickableEquipment} selected={equipmentIds} onChange={setEquipmentIds} />

      {submitError && <p className="text-sm text-red-600">{submitError}</p>}
    </form>
  );
}

function EditForm({ item, onClose, onBusy }: { item: Item; onClose: () => void; onBusy: (busy: boolean) => void }) {
  const itemId = item.id;
  const { data: categories } = useCategories();
  const updateItem = useUpdateItem();
  const qc = useQueryClient();
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [threshold, setThreshold] = useState(item.lowStockThreshold === null ? '' : String(item.lowStockThreshold));
  const [thresholdError, setThresholdError] = useState<string | null>(null);

  const tracksIngredients = useTenantSettings().data?.useSeparateInventoryTracking === true;
  const inventoryItems = useInventoryItems();
  const savedRecipe = useRecipe(tracksIngredients ? itemId : '');
  const replaceRecipe = useReplaceRecipe(itemId);
  const [recipe, setRecipe] = useState<RecipeSelection>({});
  const [recipeError, setRecipeError] = useState<{ id: string; message: string } | null>(null);
  // What the form started from, so saving only touches the recipe when it was really changed.
  const [initialRecipe, setInitialRecipe] = useState<RecipeSelection | null>(null);

  const equipment = useEquipment();
  const savedEquipment = useItemEquipment(itemId);
  const replaceEquipment = useReplaceItemEquipment();
  const [equipmentIds, setEquipmentIds] = useState<string[]>([]);
  // What the form started from, so saving only touches the equipment when it was really changed.
  const [initialEquipment, setInitialEquipment] = useState<string[] | null>(null);
  // Retired equipment stays pickable only while this item already uses it.
  const pickableEquipment = (equipment.data ?? []).filter((e) => e.isActive || (initialEquipment?.includes(e.id) ?? false));

  const busy = updateItem.isPending || replaceRecipe.isPending || replaceEquipment.isPending;
  useEffect(() => onBusy(busy), [busy, onBusy]);

  useEffect(() => {
    if (!savedEquipment.data || initialEquipment) return;
    const ids = savedEquipment.data.map((e) => e.equipmentId);
    setInitialEquipment(ids);
    setEquipmentIds(ids);
  }, [savedEquipment.data, initialEquipment]);

  useEffect(() => {
    if (!savedRecipe.data || initialRecipe) return;
    const fromServer = selectionFromRecipe(savedRecipe.data);
    setInitialRecipe(fromServer);
    setRecipe(fromServer);
  }, [savedRecipe.data, initialRecipe]);

  // An item's own paired stock record cannot be an ingredient of its own recipe. A retired one stays pickable only if already chosen.
  const pickable = (inventoryItems.data ?? []).filter((i) => i.linkedItemId !== itemId && (i.isActive || (initialRecipe !== null && i.id in initialRecipe)));

  const {
    register,
    handleSubmit,
    control,
    setValue,
    formState: { errors },
  } = useForm<EditValues>({
    resolver: zodResolver(updateItemSchema),
    values: {
      name: item.name,
      sku: item.sku,
      barcode: item.barcode,
      categoryId: item.categoryId,
      basePrice: item.basePrice,
      imageUrl: item.imageUrl,
      sortOrder: item.sortOrder,
      isActive: item.isActive,
      departmentId: item.departmentId,
    },
  });

  const imageUrl = useWatch({ control, name: 'imageUrl' });

  async function onSubmit(values: EditValues) {
    setSubmitError(null);
    setRecipeError(null);
    setThresholdError(null);
    const parsedThreshold = parseThreshold(threshold);
    if (!parsedThreshold.ok) {
      setThresholdError(parsedThreshold.message);
      return;
    }
    const recipeChanged = tracksIngredients && initialRecipe !== null && JSON.stringify(sorted(recipe)) !== JSON.stringify(sorted(initialRecipe));
    const recipeLines = recipeChanged ? buildRecipeLines(recipe) : { ok: true as const, lines: [] };
    if (!recipeLines.ok) {
      setRecipeError({ id: recipeLines.inventoryItemId, message: recipeLines.message });
      return;
    }
    const equipmentChanged = initialEquipment !== null && JSON.stringify([...equipmentIds].sort()) !== JSON.stringify([...initialEquipment].sort());
    try {
      await updateItem.mutateAsync({
        itemId,
        body: {
          name: values.name,
          sku: values.sku ?? null,
          barcode: values.barcode ?? null,
          categoryId: values.categoryId ?? null,
          basePrice: values.basePrice,
          imageUrl: values.imageUrl ?? null,
          sortOrder: values.sortOrder,
          isActive: values.isActive,
          departmentId: values.departmentId ?? null,
        },
      });
      if (parsedThreshold.value !== item.lowStockThreshold) {
        await catalogApi.updateLowStockThreshold(itemId, { threshold: parsedThreshold.value });
        await qc.invalidateQueries({ queryKey: catalogKeys.items });
      }
      // An empty list is a real change here: it turns the item back into its own inventory item.
      if (recipeChanged) await replaceRecipe.mutateAsync({ lines: recipeLines.lines });
      if (equipmentChanged) await replaceEquipment.mutateAsync({ itemId, equipmentIds });
      onClose();
    } catch (err) {
      setSubmitError(err instanceof ApiError ? err.message : 'Failed to update item');
    }
  }

  return (
    <form id={FORM_ID} onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-4">
      <div className="flex items-center gap-2">
        <StatusBadge label={pricingTypeLabels[item.pricingType]} />
        <p className="text-xs text-gray-500">Pricing type is fixed at creation and cannot be changed.</p>
      </div>

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

      <Field label="Sort order" error={errors.sortOrder?.message}>
        <input type="number" {...register('sortOrder')} className={inputClass} />
      </Field>

      <ThresholdField value={threshold} onChange={setThreshold} error={thresholdError} />

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

      {initialEquipment === null ? (
        <p className="text-sm text-gray-500">Loading equipment…</p>
      ) : (
        <EquipmentSelector equipment={pickableEquipment} selected={equipmentIds} onChange={setEquipmentIds} />
      )}

      {submitError && <p className="text-sm text-red-600">{submitError}</p>}
    </form>
  );
}
