import { Check } from '@phosphor-icons/react';
import type { ReactNode } from 'react';

interface ChoiceTileProps {
  icon: ReactNode;
  title: string;
  subtitle?: string;
  selected?: boolean;
  onSelect: () => void;
}

/** A large single-choice tile: a drawn icon, the choice, and a line saying what it means. Used for how to eat and how to pay. */
export function ChoiceTile({ icon, title, subtitle, selected = false, onSelect }: ChoiceTileProps) {
  return (
    <button
      type="button"
      aria-pressed={selected}
      onClick={onSelect}
      className={`relative flex min-h-44 w-full items-center gap-6 rounded-panel border-2 px-8 py-6 text-left transition-colors duration-150 active:translate-y-px ${
        selected ? 'border-brand bg-brand-tint' : 'border-line bg-surface hover:border-brand/60'
      }`}
    >
      <span className={`grid size-24 shrink-0 place-items-center rounded-full ${selected ? 'bg-brand text-on-brand' : 'bg-brand-tint text-brand-strong'}`}>{icon}</span>
      <span className="flex min-w-0 flex-1 flex-col gap-1">
        <span className="text-3xl font-extrabold leading-tight">{title}</span>
        {subtitle && <span className="text-xl text-ink-soft">{subtitle}</span>}
      </span>
      {selected && (
        <span aria-hidden="true" className="absolute right-5 top-5 grid size-9 place-items-center rounded-full bg-brand text-on-brand">
          <Check size={22} weight="bold" />
        </span>
      )}
    </button>
  );
}
