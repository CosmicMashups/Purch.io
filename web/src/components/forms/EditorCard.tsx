import type { FormEventHandler, ReactNode } from 'react';
import { PrimaryButton, SecondaryButton } from './FormField';

/** A titled form card with Save and Cancel, used beside a list for add/edit. */
export function EditorCard({
  title,
  editing,
  heading,
  cancelable = editing,
  busy,
  submitLabel,
  onSubmit,
  onCancel,
  children,
}: {
  title: string;
  editing: boolean;
  /** Overrides the "New x" / "Edit x" heading for cards that are neither, like a stock count. */
  heading?: string;
  cancelable?: boolean;
  busy: boolean;
  submitLabel?: string;
  onSubmit: FormEventHandler<HTMLFormElement>;
  onCancel: () => void;
  children: ReactNode;
}) {
  return (
    <form onSubmit={onSubmit} noValidate className="flex flex-col gap-4 rounded-panel border border-line bg-surface p-5 lg:self-start">
      <h3 className="text-lg font-semibold">{heading ?? (editing ? `Edit ${title}` : `New ${title}`)}</h3>
      {children}
      <div className="flex gap-3">
        <PrimaryButton type="submit" busy={busy}>
          {busy ? 'Saving...' : (submitLabel ?? (editing ? 'Save changes' : 'Add'))}
        </PrimaryButton>
        {cancelable && (
          <SecondaryButton type="button" onClick={onCancel}>
            Cancel
          </SecondaryButton>
        )}
      </div>
    </form>
  );
}
