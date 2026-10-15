import { useEffect, useId, useLayoutEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { DotsThreeVertical } from '@phosphor-icons/react';

export interface RowAction {
  label: string;
  onSelect: () => void;
  /** Red, and set apart at the bottom of the menu. */
  danger?: boolean;
  disabled?: boolean;
  /** Why it is unavailable, shown under the label. */
  hint?: string;
  /** Starts a new group: a divider is drawn above it. */
  separated?: boolean;
}

interface RowActionsMenuProps {
  /** What the menu belongs to, for the button's accessible name: "Actions for Add fries & sides". */
  subject: string;
  actions: RowAction[];
}

/**
 * The ⋯ button at the end of a row, and the actions behind it. Opens as a popover beside the button on tablets
 * and desktops and as a bottom sheet on phones, with rows at least 48px tall either way. The popover is rendered
 * at the document root and positioned from the button, so a table or card that clips its content never clips the menu. Labels are short verbs
 * because the row already says what they act on; the button carries the full name for screen readers.
 */
export function RowActionsMenu({ subject, actions }: RowActionsMenuProps) {
  const [open, setOpen] = useState(false);
  const menuId = useId();
  const root = useRef<HTMLDivElement>(null);
  const trigger = useRef<HTMLButtonElement>(null);
  const menu = useRef<HTMLDivElement>(null);
  const [position, setPosition] = useState<{ top: number; right: number } | null>(null);

  // Place the popover under the button, right edges aligned; flip above it when there is no room below.
  useLayoutEffect(() => {
    if (!open || !trigger.current) return;
    const place = () => {
      const rect = trigger.current!.getBoundingClientRect();
      const height = menu.current?.offsetHeight ?? 0;
      const below = rect.bottom + 4;
      const top = below + height > window.innerHeight - 8 && rect.top - height - 4 > 8 ? rect.top - height - 4 : below;
      setPosition({ top, right: Math.max(8, window.innerWidth - rect.right) });
    };
    place();
    window.addEventListener('resize', place);
    window.addEventListener('scroll', place, true);
    return () => {
      window.removeEventListener('resize', place);
      window.removeEventListener('scroll', place, true);
    };
  }, [open]);

  useEffect(() => {
    if (!open) return;
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setOpen(false);
        trigger.current?.focus();
      }
    };
    const onPointer = (event: PointerEvent) => {
      const target = event.target as Node;
      if (root.current && !root.current.contains(target) && !menu.current?.contains(target)) setOpen(false);
    };
    window.addEventListener('keydown', onKey);
    window.addEventListener('pointerdown', onPointer);
    menu.current?.querySelector<HTMLElement>('[role="menuitem"]:not([disabled])')?.focus();
    return () => {
      window.removeEventListener('keydown', onKey);
      window.removeEventListener('pointerdown', onPointer);
    };
  }, [open]);

  if (actions.length === 0) return null;

  return (
    <div ref={root} className="relative shrink-0">
      <button
        ref={trigger}
        type="button"
        aria-label={`Actions for ${subject}`}
        aria-haspopup="menu"
        aria-expanded={open}
        aria-controls={open ? menuId : undefined}
        onClick={() => setOpen((v) => !v)}
        className="grid size-12 place-items-center rounded-control text-ink-soft hover:bg-canvas hover:text-ink"
      >
        <DotsThreeVertical size={28} weight="bold" aria-hidden="true" />
      </button>
      {open &&
        createPortal(
        <>
          <div className="fixed inset-0 z-[55] bg-black/40 md:hidden" aria-hidden="true" />
          <div
            ref={menu}
            id={menuId}
            role="menu"
            aria-label={`Actions for ${subject}`}
            style={position ? ({ '--menu-top': `${position.top}px`, '--menu-right': `${position.right}px` } as React.CSSProperties) : undefined}
            className="fixed inset-x-0 bottom-0 z-[60] max-h-[80vh] overflow-y-auto rounded-t-panel bg-surface p-2 pb-6 shadow-xl md:fixed md:inset-auto md:right-[var(--menu-right)] md:top-[var(--menu-top)] md:min-w-60 md:rounded-control md:p-1 md:pb-1 md:ring-1 md:ring-black/10"
          >
            <p className="px-4 pb-1 pt-2 text-sm font-semibold text-ink-soft md:hidden">{subject}</p>
            {actions.map((action) => (
              <div key={action.label}>
                {action.separated && <div role="separator" className="my-1 h-px bg-line" />}
                <button
                  type="button"
                  role="menuitem"
                  disabled={action.disabled}
                  onClick={() => {
                    setOpen(false);
                    action.onSelect();
                  }}
                  className={`flex min-h-12 w-full flex-col justify-center rounded-control px-4 py-2 text-left text-base font-semibold disabled:opacity-50 ${
                    action.danger ? 'text-danger hover:bg-red-50' : 'text-ink hover:bg-canvas'
                  }`}
                >
                  {action.label}
                  {action.hint && <span className="text-sm font-normal text-ink-soft">{action.hint}</span>}
                </button>
              </div>
            ))}
          </div>
        </>,
        document.body,
      )}
    </div>
  );
}
