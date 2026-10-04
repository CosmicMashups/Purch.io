interface TabsProps<T extends string> {
  label: string;
  tabs: readonly { id: T; label: string }[];
  active: T;
  onChange: (id: T) => void;
  /** Distinguishes two tab lists on one page, so their element ids do not collide. */
  idPrefix?: string;
}

/** A row of tab buttons. The caller renders the panel and gives it id `${idPrefix}-panel`. */
export function Tabs<T extends string>({ label, tabs, active, onChange, idPrefix = 'tabs' }: TabsProps<T>) {
  return (
    <div role="tablist" aria-label={label} className="flex flex-wrap gap-2">
      {tabs.map((tab) => {
        const selected = tab.id === active;
        return (
          <button
            key={tab.id}
            type="button"
            role="tab"
            id={`${idPrefix}-${tab.id}`}
            aria-selected={selected}
            aria-controls={`${idPrefix}-panel`}
            onClick={() => onChange(tab.id)}
            className={`h-11 rounded-control px-5 text-base font-semibold ${selected ? 'bg-brand text-on-brand' : 'border border-line bg-surface hover:border-brand'}`}
          >
            {tab.label}
          </button>
        );
      })}
    </div>
  );
}
