import { useEffect, useState } from 'react';
import { useToastStore, type Toast, type ToastTone } from './toastStore';

const TONE_CLASS: Record<ToastTone, string> = {
  success: 'border-ok bg-white text-ink',
  error: 'border-danger bg-white text-ink',
  info: 'border-line bg-white text-ink',
};

const TONE_LABEL: Record<ToastTone, string> = { success: 'Done', error: 'Problem', info: 'Note' };

export function Toaster() {
  const toasts = useToastStore((s) => s.toasts);
  const dismiss = useToastStore((s) => s.dismiss);

  return (
    <div
      aria-live="polite"
      className="pointer-events-none print:hidden fixed inset-x-4 bottom-24 z-50 flex flex-col items-center gap-2 md:bottom-6 md:items-end md:right-6 md:left-auto"
    >
      {toasts.map((t) => (
        <div
          key={t.id}
          role={t.tone === 'error' ? 'alert' : 'status'}
          className={`pointer-events-auto flex w-full max-w-sm items-start gap-3 rounded-control border-l-4 p-4 shadow-lg ring-1 ring-black/5 ${TONE_CLASS[t.tone]}`}
        >
          <div className="min-w-0 flex-1">
            <p className="text-xs font-semibold uppercase tracking-wide text-ink-soft">{TONE_LABEL[t.tone]}</p>
            <p className="text-base">{t.message}</p>
          </div>
          {t.action && <UndoButton toast={t} onTaken={() => dismiss(t.id)} />}
          <button
            type="button"
            onClick={() => dismiss(t.id)}
            aria-label="Dismiss message"
            className="-m-2 grid size-12 shrink-0 place-items-center rounded-control text-ink-soft hover:bg-canvas"
          >
            <span aria-hidden="true" className="text-xl leading-none">
              ×
            </span>
          </button>
        </div>
      ))}
    </div>
  );
}

/** The action on an undoable toast, with the seconds left. The toast itself leaves when the time is up. */
function UndoButton({ toast, onTaken }: { toast: Toast; onTaken: () => void }) {
  const total = Math.ceil((toast.durationMs ?? 5000) / 1000);
  const [left, setLeft] = useState(total);

  useEffect(() => {
    const started = Date.now();
    const timer = setInterval(() => setLeft(Math.max(0, total - Math.floor((Date.now() - started) / 1000))), 250);
    return () => clearInterval(timer);
  }, [total]);

  return (
    <button
      type="button"
      onClick={() => {
        toast.action?.onAction();
        onTaken();
      }}
      className="h-12 shrink-0 rounded-control border border-line px-4 text-base font-semibold hover:border-brand"
    >
      {toast.action?.label} <span className="tabular-nums text-ink-soft">({left})</span>
    </button>
  );
}
