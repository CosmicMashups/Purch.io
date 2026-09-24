import { useState } from 'react';
import { Modal } from '../../../components/Modal';
import { FormField, PrimaryButton, controlClass } from '../../../components/forms/FormField';
import type { Item } from '../../catalog/types';
import { ScalePanel } from '../../../hardware/scale/ScalePanel';
import { useScale } from '../../../hardware/scale/scaleStore';

interface WeightDialogProps {
  item: Item;
  busy: boolean;
  onAdd: (quantity: number) => void;
  onClose: () => void;
}

/** Weight or volume entry. A connected scale fills it in once its reading has settled; typing stays available. */
export function WeightDialog({ item, busy, onAdd, onClose }: WeightDialogProps) {
  const [text, setText] = useState('');
  const scaleConnected = useScale((s) => s.status === 'connected');
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
      {scaleConnected && (
        <div className="mb-4">
          <ScalePanel onUse={(kilograms) => setText(String(kilograms))} />
        </div>
      )}
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
