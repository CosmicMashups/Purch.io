/*
 * Design note (Task 11, U1 POS). Manual discounts, price overrides, open items and notes reuse the shared Modal
 * (focus moves in, Escape closes, bottom sheet on phones) and the FormField chrome, so they read as the same
 * family as ParkCartDialog. Every target is at least 48px. Colour keeps its operational meaning: a manual discount
 * or override is marked in the amber accent (an adjustment someone chose), never the paid/success green or the
 * void red; the primary action stays teal. Reasons are chips plus free text because the same few reasons repeat
 * at a till and tapping beats typing. A live preview shows what comes off before the server confirms it.
 * Skills run: impeccable (operate mode), design-taste-frontend (pre-flight), ui-ux-pro-max (react + html-tailwind,
 * dials 6/4/3, pre-delivery checklist).
 */
import { useState } from 'react';
import { Modal } from '../../../components/Modal';
import { controlClass, FormField, PrimaryButton, SecondaryButton } from '../../../components/forms/FormField';
import { formatPeso } from '../../dashboard/format';
import { discountPreview, discountSchema, fieldErrors, parseAmount, REASON_CHIPS } from '../rules';
import type { ManualDiscountKind, ManualDiscountRequest } from '../types';


interface DiscountDialogProps {
  /** "Iced Latte", or "the whole cart". */
  subject: string;
  /** What the discount applies to: the line total, or the cart subtotal. */
  total: number;
  initial?: { kind: ManualDiscountKind; value: number; reason: string | null };
  busy: boolean;
  onApply: (body: ManualDiscountRequest) => void;
  /** Shown only when a discount is already on; sends value 0. */
  onRemove?: () => void;
  onClose: () => void;
  /** True while a dialog on top of this one (the approver PIN) owns Escape. */
  suspended?: boolean;
}

export function DiscountDialog({ subject, total, initial, busy, onApply, onRemove, onClose, suspended = false }: DiscountDialogProps) {
  const [kind, setKind] = useState<ManualDiscountKind>(initial?.kind ?? 'Percent');
  const [value, setValue] = useState(initial ? String(initial.value) : '');
  const [reason, setReason] = useState(initial?.reason ?? '');
  const [errors, setErrors] = useState<Record<string, string>>({});
  const amount = discountPreview(kind, parseAmount(value), total);

  function submit(event: React.FormEvent) {
    event.preventDefault();
    const parsed = discountSchema(total).safeParse({ kind, value: parseAmount(value), reason });
    if (!parsed.success) return setErrors(fieldErrors(parsed.error));
    setErrors({});
    onApply(parsed.data);
  }

  return (
    <Modal open title={`Discount ${subject}`} onClose={onClose} suspended={suspended}>
      <form onSubmit={submit} noValidate className="flex flex-col gap-4">
        <div role="radiogroup" aria-label="Discount type" className="grid grid-cols-2 gap-2">
          {(
            [
              ['Percent', 'Percent (%)'],
              ['Fixed', 'Peso amount (₱)'],
            ] as const
          ).map(([k, label]) => (
            <button
              key={k}
              type="button"
              role="radio"
              aria-checked={kind === k}
              onClick={() => setKind(k)}
              className={`h-12 rounded-control text-base font-semibold ${kind === k ? 'bg-brand text-on-brand' : 'border border-line hover:border-brand'}`}
            >
              {label}
            </button>
          ))}
        </div>

        <FormField label={kind === 'Percent' ? 'Percent off' : 'Amount off'} error={errors.value} hint={`Of ${formatPeso(total)}`}>
          <input inputMode="decimal" autoComplete="off" value={value} onChange={(e) => setValue(e.target.value)} className={`${controlClass} text-lg tabular-nums`} />
        </FormField>

        <div className="flex items-baseline justify-between rounded-control bg-warn/10 px-3 py-2 text-warn">
          <span className="text-sm font-semibold">Takes off</span>
          <span data-testid="discount-preview" className="text-lg font-bold tabular-nums">
            -{formatPeso(amount)}
          </span>
        </div>

        <ReasonField value={reason} onChange={setReason} error={errors.reason} />

        <div className="flex flex-wrap items-center justify-end gap-3">
          {onRemove && (
            <button type="button" disabled={busy} onClick={onRemove} className="mr-auto h-12 px-2 text-base font-semibold text-ink-soft underline hover:text-ink disabled:opacity-40">
              Remove discount
            </button>
          )}
          <SecondaryButton type="button" onClick={onClose}>
            Cancel
          </SecondaryButton>
          <PrimaryButton type="submit" busy={busy}>
            {busy ? 'Applying...' : 'Apply discount'}
          </PrimaryButton>
        </div>
      </form>
    </Modal>
  );
}

/** Chips for the usual reasons, plus free text. Tapping a chip fills the field, which stays editable. */
export function ReasonField({ value, onChange, error }: { value: string; onChange: (v: string) => void; error?: string }) {
  return (
    <div className="flex flex-col gap-2">
      <FormField label="Reason" error={error} hint={`${value.length}/200`}>
        <input maxLength={200} autoComplete="off" value={value} onChange={(e) => onChange(e.target.value)} className={controlClass} />
      </FormField>
      <div className="flex flex-wrap gap-2" aria-label="Common reasons">
        {REASON_CHIPS.map((chip) => (
          <button
            key={chip}
            type="button"
            aria-pressed={value === chip}
            onClick={() => onChange(chip)}
            className={`min-h-12 rounded-full px-4 text-sm font-semibold ${value === chip ? 'bg-brand-tint text-brand-strong ring-1 ring-brand' : 'border border-line hover:border-brand'}`}
          >
            {chip}
          </button>
        ))}
      </div>
    </div>
  );
}
