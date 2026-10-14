import { useState } from 'react';
import { RowActionsMenu, type RowAction } from '../../../components/RowActionsMenu';
import { Modal } from '../../../components/Modal';
import { controlClass, FormField, PrimaryButton, SecondaryButton } from '../../../components/forms/FormField';
import { lineNoteSchema, SENIOR_EXCLUSIVE } from '../rules';
import type { TransactionLine } from '../types';


interface LineActionsMenuProps {
  line: TransactionLine;
  /** Manager/Admin. A Cashier never sees Discount or Override price at all. */
  isSupervisor: boolean;
  seniorApplied: boolean;
  disabled: boolean;
  onDiscount: () => void;
  onOverride: () => void;
  onNote: () => void;
}

/** The per-line ⋯ menu. Remove stays a visible button on the line, as it is the most common edit. */
export function LineActionsMenu({ line, isSupervisor, seniorApplied, disabled, onDiscount, onOverride, onNote }: LineActionsMenuProps) {
  const actions: RowAction[] = [];
  if (isSupervisor) {
    actions.push(
      { label: (line.manualDiscountAmount ?? 0) > 0 ? 'Change discount' : 'Discount', onSelect: onDiscount, disabled: disabled || seniorApplied, hint: seniorApplied ? SENIOR_EXCLUSIVE : undefined },
      { label: 'Override price', onSelect: onOverride, disabled: disabled || seniorApplied, hint: seniorApplied ? SENIOR_EXCLUSIVE : undefined },
    );
  }
  actions.push({ label: line.note ? 'Edit note' : 'Add note', onSelect: onNote, disabled, separated: isSupervisor });
  return <RowActionsMenu subject={line.itemName} actions={actions} />;
}

interface LineNoteDialogProps {
  itemName: string;
  initial: string | null | undefined;
  busy: boolean;
  /** Null clears the note. */
  onSave: (note: string | null) => void;
  onClose: () => void;
}

export function LineNoteDialog({ itemName, initial, busy, onSave, onClose }: LineNoteDialogProps) {
  const [note, setNote] = useState(initial ?? '');
  const [error, setError] = useState<string>();

  function submit(event: React.FormEvent) {
    event.preventDefault();
    const parsed = lineNoteSchema.safeParse(note);
    if (!parsed.success) return setError(parsed.error.issues[0].message);
    const trimmed = note.trim();
    onSave(trimmed === '' ? null : trimmed);
  }

  return (
    <Modal open title={`Note for ${itemName}`} onClose={onClose}>
      <form onSubmit={submit} noValidate className="flex flex-col gap-4">
        <FormField label="Note" error={error} hint={`Shown under the item in the cart. ${note.length}/200`}>
          <input maxLength={200} autoComplete="off" value={note} onChange={(e) => setNote(e.target.value)} placeholder="Less ice" className={controlClass} />
        </FormField>
        <div className="flex justify-end gap-3">
          <SecondaryButton type="button" onClick={onClose}>
            Cancel
          </SecondaryButton>
          <PrimaryButton type="submit" busy={busy}>
            Save note
          </PrimaryButton>
        </div>
      </form>
    </Modal>
  );
}
