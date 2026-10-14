import { useId, useState } from 'react';
import { Modal } from '../../../components/Modal';

const PARK_LABEL_MAX = 80;

interface ParkCartDialogProps {
  open: boolean;
  busy: boolean;
  /** Called with the trimmed label, or undefined when it was left blank. */
  onPark: (label: string | undefined) => void;
  onClose: () => void;
}

/** One optional field: a name the cashier will recognise in the parked list ("Table 4", "Lola in the blue shirt"). */
export function ParkCartDialog({ open, busy, onPark, onClose }: ParkCartDialogProps) {
  const [label, setLabel] = useState('');
  const fieldId = useId();
  const helpId = useId();

  function submit(event: React.FormEvent) {
    event.preventDefault();
    const trimmed = label.trim();
    onPark(trimmed === '' ? undefined : trimmed);
  }

  return (
    <Modal open={open} title="Park this cart" onClose={onClose}>
      <form onSubmit={submit} className="flex flex-col gap-2">
        <label htmlFor={fieldId} className="text-base font-semibold">
          Label <span className="font-normal text-ink-soft">(optional)</span>
        </label>
        <input
          id={fieldId}
          aria-describedby={helpId}
          maxLength={PARK_LABEL_MAX}
          autoComplete="off"
          value={label}
          onChange={(e) => setLabel(e.target.value)}
          placeholder="Table 4"
          className="h-12 w-full rounded-control border border-ink-soft/40 bg-surface px-3 text-base"
        />
        <p id={helpId} className="text-sm text-ink-soft">
          Any register in this branch can pick it up from Parked. {label.length}/{PARK_LABEL_MAX}
        </p>
        <div className="mt-4 flex justify-end gap-3">
          <button type="button" onClick={onClose} className="h-12 rounded-control border border-line px-6 text-base font-semibold hover:border-brand">
            Cancel
          </button>
          <button type="submit" disabled={busy} className="h-12 rounded-control bg-brand px-6 text-base font-semibold text-on-brand hover:bg-brand-strong active:translate-y-px disabled:opacity-60">
            {busy ? 'Parking...' : 'Park cart'}
          </button>
        </div>
      </form>
    </Modal>
  );
}
