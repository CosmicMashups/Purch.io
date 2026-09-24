import { useState } from 'react';
import { ITEM_SAMPLES } from '../../../lib/images';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useNavigate } from 'react-router-dom';
import type { z } from 'zod';
import { createItemSchema } from '../schemas';
import { PricingType, TingiMode } from '../types';
import { pricingTypeLabels } from '../labels';
import { catalogApi } from '../api';
import { useCategories, useCreateItem } from '../queries';
import { Field, inputClass } from '../../../components/Field';
import { ImageUploadField } from '../../../components/forms/ImageUploadField';
import { ApiError } from '../../../lib/apiError';

type FormValues = z.infer<typeof createItemSchema>;

export function AddItemPage() {
  const navigate = useNavigate();
  const { data: categories } = useCategories();
  const createItem = useCreateItem();

  const {
    register,
    handleSubmit,
    watch,
    setValue,
    formState: { errors },
  } = useForm<FormValues>({
    resolver: zodResolver(createItemSchema),
    defaultValues: { pricingType: PricingType.Unit },
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

  async function createSubResourceIfValid(itemId: string, values: FormValues) {
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

  async function onSubmit(values: FormValues) {
    setSubmitError(null);
    try {
      const item = await createItem.mutateAsync({
        name: values.name,
        sku: values.sku ?? null,
        barcode: values.barcode ?? null,
        categoryId: values.categoryId ?? null,
        basePrice: values.basePrice,
        imageUrl: values.imageUrl ?? null,
        pricingType: values.pricingType,
      });

      await createSubResourceIfValid(item.id, values);

      navigate('/catalog/items');
    } catch (err) {
      setSubmitError(err instanceof ApiError ? err.message : 'Failed to create item');
    }
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="flex max-w-xl flex-col gap-4">
      <h1 className="text-xl font-semibold text-gray-900">Add Item</h1>

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
      <ImageUploadField label="Image" samples={ITEM_SAMPLES} value={imageUrl ?? null} onChange={(url) => setValue('imageUrl', url, { shouldDirty: true })} />

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

      {submitError && <p className="text-sm text-red-600">{submitError}</p>}

      <button
        type="submit"
        disabled={createItem.isPending}
        className="self-start rounded-md bg-gray-900 px-4 py-2 text-sm font-medium text-white disabled:opacity-50"
      >
        {createItem.isPending ? 'Saving…' : 'Save Item'}
      </button>
    </form>
  );
}
