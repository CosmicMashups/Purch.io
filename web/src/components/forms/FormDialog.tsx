import { useId, type FormEventHandler, type ReactNode } from 'react';
import { Modal } from '../Modal';
import { FormLoader } from './FormLoader';
import { PrimaryButton, SecondaryButton } from './FormField';

/** A form in a dialog: the fields scroll, Cancel and the submit button stay in the footer. Mount it only while it is open so its form starts fresh. */
export function FormDialog({
  title,
  submitLabel,
  busy,
  wide = false,
  onSubmit,
  onClose,
  children,
}: {
  title: string;
  submitLabel: string;
  busy: boolean;
  wide?: boolean;
  onSubmit: FormEventHandler<HTMLFormElement>;
  onClose: () => void;
  children: ReactNode;
}) {
  const formId = useId();
  return (
    <Modal
      open
      wide={wide}
      title={title}
      onClose={onClose}
      footer={
        <div className="flex justify-end gap-3">
          <SecondaryButton type="button" onClick={onClose}>
            Cancel
          </SecondaryButton>
          <PrimaryButton type="submit" form={formId} busy={busy}>
            {busy ? 'Saving...' : submitLabel}
          </PrimaryButton>
        </div>
      }
    >
      <form id={formId} onSubmit={onSubmit} noValidate className="flex flex-col gap-4">
        {children}
      </form>
    </Modal>
  );
}

/** Shows a dialog with a skeleton or an error until the data a form's defaults depend on has loaded, then hands over to the form's own FormDialog. */
export function FormDialogLoader({
  title,
  ready,
  failed,
  onClose,
  children,
}: {
  title: string;
  ready: boolean;
  failed: { error: unknown; refetch: () => unknown } | null;
  onClose: () => void;
  children: ReactNode;
}) {
  if (!failed && ready) return <>{children}</>;
  return (
    <Modal open title={title} onClose={onClose}>
      <FormLoader ready={ready} failed={failed}>
        {null}
      </FormLoader>
    </Modal>
  );
}
