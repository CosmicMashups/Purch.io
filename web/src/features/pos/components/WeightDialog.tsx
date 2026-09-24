import { useState } from 'react';
import { Modal } from '../../../components/Modal';
import { FormField, PrimaryButton, controlClass } from '../../../components/forms/FormField';
import type { Item } from '../../catalog/types';

interface WeightDialogProps {
  item: Item;
  busy: boolean;
  onAdd: (quantity: number) => void;
  onClose: () => void;
}

/** Manual weight or volume entry. A hardware scale can fill this in later. */
export function WeightDialog({ item, busy, onAdd, onClose }: WeightDialogProps) {
  const [text, setText] = useState('');
  const quantity = Number(text);
  const valid = text.trim() !== '' && Number.isFinite(quantity) && quantity > 0;

  return (
    <Modal
      open
      title={item.name}
      onClose={onClose}
      footer={
        <PrimaryButton type="button" busy={busy} disabled={!valid} onClick={() => onAdd(quantity)}>
          {busy ? 'Adding...' : 'Add to cart'}
        </PrimaryButton>
      }
    >
      <form
        onSubmit={(e) => {
          e.preventDefault();
          if (valid) onAdd(quantity);
        }}
      >
        <FormField label="Weight or amount" hint={item.packagedSize ? `A full pack is ${item.packagedSize}` : 'Enter what the customer is buying'}>
          <input inputMode="decimal" value={text} onChange={(e) => setText(e.target.value)} className={controlClass} />
        </FormField>
      </form>
    </Modal>
  );
}
