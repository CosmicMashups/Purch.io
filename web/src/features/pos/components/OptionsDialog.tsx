import { useState } from 'react';
import { ErrorState } from '../../../components/ErrorState';
import { Modal } from '../../../components/Modal';
import { Skeleton } from '../../../components/Skeleton';
import { PrimaryButton, controlClass } from '../../../components/forms/FormField';
import { userMessage } from '../../../lib/apiError';
import { useComboComponents, useItemModifierGroups, useVariants } from '../../catalog/queries';
import { PricingType, type Item } from '../../catalog/types';
import { formatPeso } from '../../dashboard/format';
import { buildAddLine, missingOption, toggleModifier, type OptionPicks, type OptionShape } from '../options';
import type { AddLineRequest } from '../types';

interface OptionsDialogProps {
  item: Item;
  /** Catalog items, for the choices behind each combo slot. */
  items: Item[];
  busy: boolean;
  onAdd: (request: AddLineRequest) => void;
  onClose: () => void;
}

const stepper = 'grid size-14 place-items-center rounded-control border border-line text-2xl font-semibold hover:border-brand disabled:opacity-40';

export function OptionsDialog({ item, items, busy, onAdd, onClose }: OptionsDialogProps) {
  const needsVariant = item.pricingType === PricingType.VariantMatrix;
  const isCombo = item.pricingType === PricingType.Combo;
  const variants = useVariants(item.id);
  const slots = useComboComponents(item.id);
  const groups = useItemModifierGroups(item.id);
  const [picks, setPicks] = useState<OptionPicks>({ variantId: null, slots: {}, groups: {}, quantity: 1 });

  const failed = (needsVariant && variants.isError ? variants : null) ?? (isCombo && slots.isError ? slots : null) ?? (groups.isError ? groups : null);
  const ready = (!needsVariant || variants.data) && (!isCombo || slots.data) && groups.data;

  const shape: OptionShape | null = ready
    ? { needsVariant, variants: variants.data ?? [], slots: isCombo ? (slots.data ?? []) : [], groups: groups.data ?? [] }
    : null;
  const problem = shape ? missingOption(shape, picks) : null;

  return (
    <Modal
      open
      title={item.name}
      onClose={onClose}
      footer={
        shape && (
          <div className="flex flex-col gap-3">
            {problem && (
              <p role="status" className="text-base font-medium text-ink-soft">
                {problem}
              </p>
            )}
            <PrimaryButton type="button" busy={busy} disabled={problem !== null} onClick={() => onAdd(buildAddLine(item.id, shape, picks))}>
              {busy ? 'Adding...' : 'Add to cart'}
            </PrimaryButton>
          </div>
        )
      }
    >
      {failed ? (
        <ErrorState title="The options could not be loaded" message={userMessage(failed.error)} onRetry={() => void failed.refetch()} />
      ) : !shape ? (
        <div className="flex flex-col gap-3" aria-busy="true">
          <Skeleton className="h-14 w-full" />
          <Skeleton className="h-14 w-full" />
        </div>
      ) : (
        <div className="flex flex-col gap-6">
          {shape.needsVariant && (
            <fieldset className="flex flex-col gap-2">
              <legend className="mb-1 text-base font-semibold">Variant</legend>
              {shape.variants.length === 0 && <p className="text-base text-ink-soft">This item has no variants set up yet.</p>}
              {shape.variants.map((variant) => (
                <label key={variant.id} className="flex min-h-14 cursor-pointer items-center gap-3 rounded-control border border-line px-4 has-[:checked]:border-brand has-[:checked]:bg-brand-tint">
                  <input
                    type="radio"
                    name="variant"
                    checked={picks.variantId === variant.id}
                    onChange={() => setPicks((p) => ({ ...p, variantId: variant.id }))}
                    className="size-5 accent-brand"
                  />
                  <span className="flex-1 text-base font-medium">{Object.values(variant.attributes).join(', ') || variant.sku || 'Variant'}</span>
                  {variant.priceOverride !== null && <span className="text-base tabular-nums">{formatPeso(variant.priceOverride)}</span>}
                </label>
              ))}
            </fieldset>
          )}

          {shape.slots.map((slot) => (
            <fieldset key={slot.id} className="flex flex-col gap-2">
              <legend className="mb-1 text-base font-semibold">{slot.slotLabel}</legend>
              {Array.from({ length: slot.quantity }, (_, index) => (
                <select
                  key={index}
                  aria-label={`${slot.slotLabel}${slot.quantity > 1 ? ` ${index + 1}` : ''}`}
                  value={picks.slots[slot.id]?.[index] ?? ''}
                  onChange={(e) =>
                    setPicks((p) => {
                      const next = [...(p.slots[slot.id] ?? Array.from({ length: slot.quantity }, () => ''))];
                      next[index] = e.target.value;
                      return { ...p, slots: { ...p.slots, [slot.id]: next } };
                    })
                  }
                  className={controlClass}
                >
                  <option value="">Choose</option>
                  {items
                    .filter((candidate) => candidate.isActive && candidate.categoryId === slot.componentCategoryId)
                    .map((candidate) => (
                      <option key={candidate.id} value={candidate.id}>
                        {candidate.name}
                      </option>
                    ))}
                </select>
              ))}
              {slot.substitutionUpchargeAmount > 0 && (
                <p className="text-sm text-ink-soft">Swapping from the usual choice can add {formatPeso(slot.substitutionUpchargeAmount)}.</p>
              )}
            </fieldset>
          ))}

          {shape.groups.map((group) => (
            <fieldset key={group.id} className="flex flex-col gap-2">
              <legend className="mb-1 text-base font-semibold">
                {group.name}
                <span className="ml-2 text-sm font-normal text-ink-soft">{group.isRequired ? 'Required' : 'Optional'}</span>
              </legend>
              {group.modifiers.map((modifier) => {
                const selected = (picks.groups[group.id] ?? []).includes(modifier.id);
                return (
                  <label key={modifier.id} className="flex min-h-14 cursor-pointer items-center gap-3 rounded-control border border-line px-4 has-[:checked]:border-brand has-[:checked]:bg-brand-tint">
                    <input
                      type={group.allowMultipleSelection ? 'checkbox' : 'radio'}
                      name={`group-${group.id}`}
                      checked={selected}
                      onChange={() => setPicks((p) => ({ ...p, groups: { ...p.groups, [group.id]: toggleModifier(group, p.groups[group.id] ?? [], modifier.id) } }))}
                      className="size-5 accent-brand"
                    />
                    <span className="flex-1 text-base font-medium">{modifier.name}</span>
                    {modifier.priceDelta !== 0 && (
                      <span className="text-base tabular-nums text-ink-soft">
                        {modifier.priceDelta > 0 ? '+' : ''}
                        {formatPeso(modifier.priceDelta)}
                      </span>
                    )}
                  </label>
                );
              })}
            </fieldset>
          ))}

          <div className="flex items-center justify-between gap-4">
            <span className="text-base font-semibold">Quantity</span>
            <div className="flex items-center gap-3">
              <button type="button" aria-label="Decrease quantity" className={stepper} disabled={picks.quantity <= 1} onClick={() => setPicks((p) => ({ ...p, quantity: p.quantity - 1 }))}>
                -
              </button>
              <span aria-live="polite" className="min-w-8 text-center text-2xl font-bold tabular-nums">
                {picks.quantity}
              </span>
              <button type="button" aria-label="Increase quantity" className={stepper} onClick={() => setPicks((p) => ({ ...p, quantity: p.quantity + 1 }))}>
                +
              </button>
            </div>
          </div>
        </div>
      )}
    </Modal>
  );
}
