import { ArrowsClockwise, CaretDown } from '@phosphor-icons/react';
import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';

export interface MenuAction {
  label: string;
  to?: string;
  onSelect?: () => void;
}

const barButton = 'inline-flex h-10 items-center gap-2 rounded-control border border-line bg-surface px-4 text-sm font-semibold hover:border-brand';

/** The Cashier page's own bar: who is signed in, the Kiosk queue, a price refresh, and the less-used till pages in a menu. */
export function CashierTopBar({ role, kioskTo, onRefresh, actions }: { role: string | null; kioskTo: string; onRefresh: () => void; actions: MenuAction[] }) {
  return (
    <div className="flex items-center justify-between gap-3 rounded-panel border border-line bg-surface px-4 py-2">
      <p className="min-w-0 truncate text-sm text-ink-soft">{role ? `Signed in as ${role}` : 'Cashier'}</p>
      <div className="flex shrink-0 items-center gap-2">
        <Link to={kioskTo} className={barButton}>
          Kiosk
        </Link>
        <button type="button" onClick={onRefresh} aria-label="Refresh prices" title="Refresh prices" className="grid size-10 place-items-center rounded-control border border-line bg-surface hover:border-brand">
          <ArrowsClockwise size={20} aria-hidden="true" />
        </button>
        <Menu actions={actions} />
      </div>
    </div>
  );
}

function Menu({ actions }: { actions: MenuAction[] }) {
  const [open, setOpen] = useState(false);
  const root = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    const close = (e: Event) => {
      if (e instanceof KeyboardEvent ? e.key === 'Escape' : !root.current?.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener('pointerdown', close);
    document.addEventListener('keydown', close);
    return () => {
      document.removeEventListener('pointerdown', close);
      document.removeEventListener('keydown', close);
    };
  }, [open]);

  const item = 'block w-full rounded-control px-3 py-2.5 text-left text-sm font-medium hover:bg-brand-tint';

  return (
    <div ref={root} className="relative">
      <button type="button" aria-haspopup="menu" aria-expanded={open} onClick={() => setOpen((o) => !o)} className={barButton}>
        More
        <CaretDown size={16} aria-hidden="true" />
      </button>
      {open && (
        <div role="menu" className="absolute right-0 top-12 z-30 flex w-52 flex-col rounded-panel border border-line bg-surface p-1 shadow-lg">
          {actions.map((a) =>
            a.to ? (
              <Link key={a.label} role="menuitem" to={a.to} className={item} onClick={() => setOpen(false)}>
                {a.label}
              </Link>
            ) : (
              <button
                key={a.label}
                type="button"
                role="menuitem"
                className={item}
                onClick={() => {
                  setOpen(false);
                  a.onSelect?.();
                }}
              >
                {a.label}
              </button>
            ),
          )}
        </div>
      )}
    </div>
  );
}
