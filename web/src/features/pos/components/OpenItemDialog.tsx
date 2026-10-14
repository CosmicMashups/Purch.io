import { useState } from 'react';
import { Modal } from '../../../components/Modal';
import { controlClass, FormField, PrimaryButton, SecondaryButton } from '../../../components/forms/FormField';
import { formatPeso } from '../../dashboard/format';
import { fieldErrors, openItemSchema, parseAmount } from '../rules';

export interface OpenItemInput {
  name: string;
  unitPrice: number;
  quantity: number;
  note?: string;
}

interface OpenItemDialogProps {
  busy: boolean;
  onAdd: (item: OpenItemInput) => void;
  onClose: () => void;
}

/**
 * Rings up something that is not in the catalog. Plain inputs with a decimal keypad rather than CashKeypad: that
 * keypad is built around a tender and its change, and three fields here would each need their own.
 */
export function OpenItemDialog({ busy, onAdd, onClose }: OpenItemDialogProps) {
  const [name, setName] = useState('');
  const [price, setPrice] = useState('');
  const [quantity, setQuantity] = useState('1');
  const [note, setNote] = useState('');
  const [errors, setErrors] = useState<Record<string, string>>({});
  const total = parseAmount(price) * parseAmount(quantity);

  function submit(event: React.FormEvent) {
    event.preventDefault();
    const parsed = openItemSchema.safeParse({ name, unitPrice: parseAmount(price), quantity: parseAmount(quantity), note });
    if (!parsed.success) return setErrors(fieldErrors(parsed.error));
    setErrors({});
    const trimmedNote = parsed.data.note?.trim();
    onAdd({ name: parsed.data.name, unitPrice: parsed.data.unitPrice, quantity: parsed.data.quantity, note: trimmedNote ? trimmedNote : undefined });
  }

  return (
    <Modal open title="Add an open item" onClose={onClose}>
      <form onSubmit={submit} noValidate className="flex flex-col gap-4">
        <FormField label="Name" error={errors.name}>
          <input maxLength={80} autoComplete="off" value={name} onChange={(e) => setName(e.target.value)} placeholder="Gift wrap" className={controlClass} />
        </FormField>
        <div className="grid grid-cols-2 gap-3">
          <FormField label="Price" error={errors.unitPrice}>
            <input inputMode="decimal" autoComplete="off" value={price} onChange={(e) => setPrice(e.target.value)} className={`${controlClass} tabular-nums`} />
          </FormField>
          <FormField label="Quantity" error={errors.quantity}>
            <input inputMode="decimal" autoComplete="off" value={quantity} onChange={(e) => setQuantity(e.target.value)} className={`${controlClass} tabular-nums`} />
          </FormField>
        </div>
        <FormField label="Note (optional)" error={errors.note} hint={`${note.length}/200`}>
          <input maxLength={200} autoComplete="off" value={note} onChange={(e) => setNote(e.target.value)} className={controlClass} />
        </FormField>
        <div className="flex flex-wrap items-center justify-end gap-3">
          <p className="mr-auto text-base font-semibold tabular-nums">{Number.isFinite(total) && total > 0 ? formatPeso(total) : ''}</p>
          <SecondaryButton type="button" onClick={onClose}>
            Cancel
          </SecondaryButton>
          <PrimaryButton type="submit" busy={busy}>
            {busy ? 'Adding...' : 'Add to cart'}
          </PrimaryButton>
        </div>
      </form>
    </Modal>
  );
}
