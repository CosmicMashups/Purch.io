import { useEffect, useId, useRef, useState, type ReactNode } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from '../../components/feedback/toastStore';
import { userMessage } from '../../lib/apiError';
import { lifecycleApi, type LifecycleAction, type LifecycleKind, type LifecycleStatus } from './api';

export const lifecycleKeys = {
  impact: (kind: LifecycleKind, id: string) => ['lifecycle', kind, id, 'impact'] as const,
  deleted: (kind: LifecycleKind) => ['lifecycle', kind, 'deleted'] as const,
};

interface Subject {
  kind: LifecycleKind;
  id: string;
  /** What the person calls it: shown in the dialog and the snackbar. */
  name: string;
}

interface Pending extends Subject {
  action: 'deactivate' | 'delete';
}

/** What a record is called in sentences: "Coffee" is fine, a device or person needs the word too. */
const NOUN: Record<LifecycleKind, string> = {
  Item: 'item',
  Ingredient: 'ingredient',
  Category: 'category',
  Supplier: 'supplier',
  ModifierGroup: 'modifier group',
  Modifier: 'modifier',
  BogoPromo: 'promotion',
  ComboPromo: 'promotion',
  ItemDiscountPromo: 'promotion',
  PromoCode: 'promo code',
  Staff: 'person',
  Branch: 'branch',
  Device: 'device',
  Customer: 'customer',
};

/**
 * Deactivate, reactivate, delete and restore for any record. Deactivate and delete ask first (naming what depends on
 * the record, or why it can't be done), then show a snackbar with a five-second Undo. Reactivate and restore just
 * happen. Put {dialog} somewhere in the page and call run() from a row menu.
 */
export function useLifecycle(): { run: (subject: Subject, action: LifecycleAction) => void; dialog: ReactNode; busy: boolean } {
  const queryClient = useQueryClient();
  const [pending, setPending] = useState<Pending | null>(null);

  const refresh = () => queryClient.invalidateQueries();

  const act = useMutation({
    mutationFn: ({ kind, id, action }: Subject & { action: LifecycleAction }) => lifecycleApi.act(kind, id, action),
  });

  async function apply(subject: Subject, action: LifecycleAction, previous?: LifecycleStatus) {
    try {
      await act.mutateAsync({ ...subject, action });
    } catch (error) {
      toast.error(userMessage(error));
      return false;
    }
    await refresh();

    const noun = NOUN[subject.kind];
    if (action === 'deactivate') {
      toast.undoable(`${subject.name} is now inactive`, () => void undo(subject, 'reactivate'));
    } else if (action === 'delete') {
      toast.undoable(`${subject.name} was deleted`, () => void undo(subject, 'restore', previous));
    } else if (action === 'reactivate') {
      toast.success(`${subject.name} is active again`);
    } else {
      toast.success(`${subject.name} was restored as an inactive ${noun}. Make it active when you are ready.`);
    }
    return true;
  }

  async function undo(subject: Subject, action: 'reactivate' | 'restore', previous?: LifecycleStatus) {
    try {
      await lifecycleApi.act(subject.kind, subject.id, action);
      // A deleted record comes back inactive; if it was live before the delete, put it back live.
      if (action === 'restore' && previous === 'Active') await lifecycleApi.act(subject.kind, subject.id, 'reactivate');
      await refresh();
      toast.info(`${subject.name} is back`);
    } catch (error) {
      toast.error(userMessage(error));
    }
  }

  function run(subject: Subject, action: LifecycleAction) {
    if (action === 'deactivate' || action === 'delete') setPending({ ...subject, action });
    else void apply(subject, action);
  }

  const dialog = pending && (
    <LifecycleDialog
      pending={pending}
      busy={act.isPending}
      onCancel={() => setPending(null)}
      onConfirm={async (previous) => {
        const { action, ...subject } = pending;
        const ok = await apply(subject, action, previous);
        if (ok) setPending(null);
      }}
    />
  );

  return { run, dialog, busy: act.isPending };
}

function LifecycleDialog({
  pending,
  busy,
  onConfirm,
  onCancel,
}: {
  pending: Pending;
  busy: boolean;
  onConfirm: (previous: LifecycleStatus | undefined) => void;
  onCancel: () => void;
}) {
  const titleId = useId();
  const cancelRef = useRef<HTMLButtonElement>(null);
  const impact = useQuery({
    queryKey: lifecycleKeys.impact(pending.kind, pending.id),
    queryFn: () => lifecycleApi.impact(pending.kind, pending.id),
    gcTime: 0,
  });

  useEffect(() => {
    cancelRef.current?.focus();
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onCancel();
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [onCancel]);

  const deleting = pending.action === 'delete';
  const noun = NOUN[pending.kind];
  const blocked = deleting ? impact.data?.deleteBlockedReason : impact.data?.deactivateBlockedReason;
  const title = deleting ? `Delete ${pending.name}?` : `Make ${pending.name} inactive?`;
  const lead = deleting
    ? `This ${noun} will be hidden everywhere and moved to Deleted. Past sales and receipts keep it. You can restore it later.`
    : `This ${noun} will stop being available. You can make it active again at any time.`;

  return (
    <div className="fixed inset-0 z-50 flex items-end justify-center bg-black/50 sm:items-center sm:px-4">
      <div role="dialog" aria-modal="true" aria-labelledby={titleId} className="w-full max-w-md rounded-t-panel bg-surface p-6 shadow-xl sm:rounded-panel">
        <h2 id={titleId} className="text-xl font-bold">
          {title}
        </h2>
        {blocked ? (
          <p role="alert" className="mt-2 text-base text-danger">
            {blocked}
          </p>
        ) : (
          <>
            <p className="mt-2 text-base text-ink-soft">{lead}</p>
            {impact.isPending && <p className="mt-3 text-sm text-ink-soft">Checking what depends on it…</p>}
            {impact.isError && <p className="mt-3 text-sm text-ink-soft">Couldn&apos;t check what depends on it. You can still continue.</p>}
            {impact.data && impact.data.notes.length > 0 && (
              <ul className="mt-3 list-disc pl-5 text-base">
                {impact.data.notes.map((note) => (
                  <li key={note}>{note}</li>
                ))}
              </ul>
            )}
          </>
        )}
        <div className="mt-6 flex flex-col-reverse gap-3 sm:flex-row sm:justify-end">
          <button ref={cancelRef} type="button" onClick={onCancel} className="h-12 rounded-control border border-line px-6 text-base font-semibold hover:border-brand">
            {blocked ? 'Close' : 'Cancel'}
          </button>
          {!blocked && (
            <button
              type="button"
              disabled={busy || impact.isPending}
              onClick={() => onConfirm(impact.data?.status)}
              className={`h-12 rounded-control px-6 text-base font-semibold text-white disabled:opacity-60 ${deleting ? 'bg-danger hover:bg-red-700' : 'bg-brand text-on-brand hover:bg-brand-strong'}`}
            >
              {deleting ? 'Delete' : 'Make inactive'}
            </button>
          )}
        </div>
      </div>
    </div>
  );
}

/** The deleted records of one kind, for a list's Deleted view. Only fetched when that view is open. */
export function useDeletedRecords(kind: LifecycleKind, enabled: boolean) {
  return useQuery({ queryKey: lifecycleKeys.deleted(kind), queryFn: () => lifecycleApi.deleted(kind), enabled });
}
