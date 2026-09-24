import { useEffect, useId, useRef } from 'react';

interface ConfirmModalProps {
  open: boolean;
  title: string;
  description?: string;
  confirmLabel?: string;
  destructive?: boolean;
  busy?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}

/**
 * A blocking confirmation. Focus starts on Cancel so an accidental Enter never confirms a
 * destructive action, and Escape always backs out.
 */
export function ConfirmModal({ open, title, description, confirmLabel = 'Confirm', destructive = false, busy = false, onConfirm, onCancel }: ConfirmModalProps) {
  const titleId = useId();
  const descriptionId = useId();
  const cancelRef = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    if (!open) return;
    cancelRef.current?.focus();
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onCancel();
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [open, onCancel]);

  if (!open) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 px-4">
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        aria-describedby={description ? descriptionId : undefined}
        className="w-full max-w-md rounded-panel bg-surface p-6 shadow-xl"
      >
        <h2 id={titleId} className="text-xl font-bold">
          {title}
        </h2>
        {description && (
          <p id={descriptionId} className="mt-2 text-base text-ink-soft">
            {description}
          </p>
        )}
        <div className="mt-6 flex justify-end gap-3">
          <button
            ref={cancelRef}
            type="button"
            onClick={onCancel}
            className="h-12 rounded-control border border-line px-6 text-base font-semibold hover:border-brand"
          >
            Cancel
          </button>
          <button
            type="button"
            disabled={busy}
            onClick={onConfirm}
            className={`h-12 rounded-control px-6 text-base font-semibold disabled:opacity-60 ${destructive ? 'bg-danger text-white hover:bg-red-700' : 'bg-brand text-on-brand hover:bg-brand-strong'}`}
          >
            {confirmLabel}
          </button>
        </div>
      </div>
    </div>
  );
}
