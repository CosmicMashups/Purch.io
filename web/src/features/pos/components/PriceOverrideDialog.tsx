import { useState } from 'react';
import { Modal } from '../../../components/Modal';
import { controlClass, FormField, PrimaryButton, SecondaryButton } from '../../../components/forms/FormField';
import { formatPeso } from '../../dashboard/format';
import { fieldErrors, parseAmount, priceOverrideSchema } from '../rules';
import type { OverridePriceRequest } from '../types';
import { ReasonField } from './DiscountDialog';

interface PriceOverrideDialogProps {
  itemName: string;
  currentUnitPrice: number;
  /** The catalog price it was first overridden from, if it already was. */
  originalUnitPrice?: number | null;
  busy: boolean;
  onApply: (body: OverridePriceRequest) => void;
  onClose: () => void;
  suspended?: boolean;
}

/** Manager/Admin only. Sets a new unit price for one line; the reason goes on the audit trail. */
export function PriceOverrideDialog({ itemName, currentUnitPrice, originalUnitPrice, busy, onApply, onClose, suspended = false }: PriceOverrideDialogProps) {
  const [price, setPrice] = useState('');
  const [reason, setReason] = useState('');
  const [errors, setErrors] = useState<Record<string, string>>({});

  function submit(event: React.FormEvent) {
    event.preventDefault();
    const parsed = priceOverrideSchema.safeParse({ newUnitPrice: parseAmount(price), reason });
    if (!parsed.success) return setErrors(fieldErrors(parsed.error));
    setErrors({});
    onApply(parsed.data);
  }

  return (
    <Modal open title={`Override price of ${itemName}`} onClose={onClose} suspended={suspended}>
      <form onSubmit={submit} noValidate className="flex flex-col gap-4">
        <p className="text-base text-ink-soft">
          Now {formatPeso(currentUnitPrice)} each
          {originalUnitPrice != null && originalUnitPrice !== currentUnitPrice && <span> (catalog price {formatPeso(originalUnitPrice)})</span>}
        </p>
        <FormField label="New unit price" error={errors.newUnitPrice}>
          <input inputMode="decimal" autoComplete="off" value={price} onChange={(e) => setPrice(e.target.value)} className={`${controlClass} text-lg tabular-nums`} />
        </FormField>
        <ReasonField value={reason} onChange={setReason} error={errors.reason} />
        <p className="text-sm text-ink-soft">A manager or admin PIN is needed to approve this.</p>
        <div className="flex justify-end gap-3">
          <SecondaryButton type="button" onClick={onClose}>
            Cancel
          </SecondaryButton>
          <PrimaryButton type="submit" busy={busy} disabled={suspended}>
            {busy ? 'Saving...' : 'Override price'}
          </PrimaryButton>
        </div>
      </form>
    </Modal>
  );
}
