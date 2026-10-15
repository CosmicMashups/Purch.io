interface SegmentedToggleProps<T extends string> {
  label: string;
  value: T;
  options: readonly { id: T; label: string }[];
  onChange: (id: T) => void;
  className?: string;
}

/**
 * A choice among a few options. It is a filter, so it is outlined and the selection is a tint: filled buttons
 * are reserved for actions.
 */
export function SegmentedToggle<T extends string>({ label, value, options, onChange, className = '' }: SegmentedToggleProps<T>) {
  return (
    <div role="group" aria-label={label} className={`inline-flex w-fit max-w-full flex-wrap overflow-hidden rounded-control border border-line print:hidden ${className}`}>
      {options.map((o) => (
        <button
          key={o.id}
          type="button"
          aria-pressed={value === o.id}
          onClick={() => onChange(o.id)}
          className={`h-12 px-4 text-base font-semibold ${value === o.id ? 'bg-brand-tint text-brand-strong' : 'bg-surface text-ink-soft hover:bg-canvas hover:text-ink'}`}
        >
          {o.label}
        </button>
      ))}
    </div>
  );
}
