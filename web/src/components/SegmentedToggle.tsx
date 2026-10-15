interface SegmentedToggleProps<T extends string> {
  label: string;
  value: T;
  options: readonly { id: T; label: string }[];
  onChange: (id: T) => void;
  className?: string;
}

/**
 * A quiet choice among a few options: a hairline border with an ink selection. It deliberately never uses the
 * brand fill, which is kept for the controls that decide what a page is showing, so "which view" and "which
 * report" cannot be mistaken for each other.
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
          className={`h-12 px-4 text-base font-semibold ${value === o.id ? 'bg-ink text-canvas' : 'bg-surface hover:bg-canvas'}`}
        >
          {o.label}
        </button>
      ))}
    </div>
  );
}
