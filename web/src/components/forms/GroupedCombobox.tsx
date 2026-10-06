import { Controller, type Control, type FieldPath, type FieldValues } from 'react-hook-form';
import { useEffect, useId, useMemo, useRef, useState } from 'react';
import { controlClass } from './FormField';

export interface ComboboxOption {
  value: string;
  label: string;
  /** Shown to the right of the label, like a unit or stock count. */
  hint?: string;
  /** Extra text the search also matches, like a SKU. */
  keywords?: string;
}

export interface ComboboxGroup {
  label: string;
  options: ComboboxOption[];
}

interface GroupedComboboxProps {
  groups: ComboboxGroup[];
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  /** Adds a first option with an empty value, for filters ("All items"). */
  emptyLabel?: string;
  /** Wired up by FormField. */
  id?: string;
  'aria-invalid'?: boolean;
  'aria-describedby'?: string;
}

/**
 * A single-choice picker that is a text box and a dropdown in one: type to narrow the list, or open it and pick. Options sit under their
 * group headings (a category), and a heading with no matches disappears. Arrow keys move, Enter picks, Escape closes.
 */
export function GroupedCombobox({ groups, value, onChange, placeholder, emptyLabel, id, ...aria }: GroupedComboboxProps) {
  const generated = useId();
  const inputId = id ?? `${generated}-input`;
  const listId = `${generated}-list`;
  const root = useRef<HTMLDivElement>(null);
  // null: showing the chosen option's label; a string: the person is typing a search.
  const [query, setQuery] = useState<string | null>(null);
  const [open, setOpen] = useState(false);
  const [active, setActive] = useState(0);

  const selected = useMemo(() => groups.flatMap((g) => g.options).find((o) => o.value === value), [groups, value]);

  const visible = useMemo(() => {
    const needle = (query ?? '').trim().toLowerCase();
    const filtered =
      needle === ''
        ? groups
        : groups
            .map((g) => ({ ...g, options: g.options.filter((o) => `${o.label} ${o.keywords ?? ''} ${g.label}`.toLowerCase().includes(needle)) }))
            .filter((g) => g.options.length > 0);
    const empty: ComboboxOption[] = emptyLabel && needle === '' ? [{ value: '', label: emptyLabel }] : [];
    const plain = [...(empty.length > 0 ? [{ label: '', options: empty }] : []), ...filtered];
    const flat = plain.flatMap((s) => s.options);
    // Each option's position in the flat list is what the keyboard moves through.
    let next = 0;
    const sections = plain.map((section) => ({ label: section.label, options: section.options.map((option) => ({ option, position: next++ })) }));
    return { sections, flat };
  }, [groups, query, emptyLabel]);

  useEffect(() => {
    if (!open) return;
    const close = (event: MouseEvent) => {
      if (!root.current?.contains(event.target as Node)) {
        setOpen(false);
        setQuery(null);
      }
    };
    document.addEventListener('mousedown', close);
    return () => document.removeEventListener('mousedown', close);
  }, [open]);

  function pick(option: ComboboxOption) {
    onChange(option.value);
    setQuery(null);
    setOpen(false);
  }

  function onKeyDown(event: React.KeyboardEvent<HTMLInputElement>) {
    if (event.key === 'Escape') {
      if (open) event.stopPropagation();
      setOpen(false);
      setQuery(null);
    } else if (event.key === 'ArrowDown') {
      event.preventDefault();
      setOpen(true);
      setActive((a) => Math.min(a + 1, Math.max(visible.flat.length - 1, 0)));
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      setActive((a) => Math.max(a - 1, 0));
    } else if (event.key === 'Enter' && open) {
      // Enter inside a form would submit it; here it picks the highlighted option.
      event.preventDefault();
      const target = visible.flat[active];
      if (target) pick(target);
    }
  }

  const optionId = (index: number) => `${generated}-option-${index}`;

  return (
    <div ref={root} className="relative">
      <input
        {...aria}
        id={inputId}
        role="combobox"
        aria-expanded={open}
        aria-controls={listId}
        aria-autocomplete="list"
        aria-activedescendant={open && visible.flat[active] ? optionId(active) : undefined}
        autoComplete="off"
        placeholder={placeholder}
        value={query ?? selected?.label ?? ''}
        onChange={(e) => {
          setQuery(e.target.value);
          setActive(0);
          setOpen(true);
        }}
        onFocus={(e) => e.currentTarget.select()}
        onClick={() => setOpen(true)}
        onKeyDown={onKeyDown}
        className={`${controlClass} pr-9`}
      />
      <span aria-hidden="true" className="pointer-events-none absolute inset-y-0 right-3 flex items-center text-ink-soft">
        ▾
      </span>
      {open && (
        <ul
          id={listId}
          role="listbox"
          aria-label={placeholder ?? 'Options'}
          className="absolute z-30 mt-1 max-h-72 w-full overflow-auto rounded-control border border-line bg-surface py-1 shadow-lg"
        >
          {visible.flat.length === 0 && <li className="px-3 py-2 text-base text-ink-soft">Nothing matches</li>}
          {visible.sections.map((section) => (
            <li key={section.label || 'ungrouped'} role="presentation">
              {section.label !== '' && (
                <p className="px-3 pb-1 pt-2 text-xs font-semibold uppercase tracking-wide text-ink-soft">{section.label}</p>
              )}
              <ul role="presentation">
                {section.options.map(({ option, position }) => {
                  return (
                    <li
                      key={option.value || 'empty'}
                      id={optionId(position)}
                      role="option"
                      aria-selected={option.value === value}
                      // mousedown would blur the input before the click lands, closing the list mid-selection.
                      onMouseDown={(e) => e.preventDefault()}
                      onClick={() => pick(option)}
                      onMouseEnter={() => setActive(position)}
                      className={`flex min-h-11 cursor-pointer items-center gap-3 px-3 py-2 text-base ${position === active ? 'bg-canvas' : ''} ${
                        option.value === value ? 'font-semibold' : ''
                      }`}
                    >
                      <span className="flex-1">{option.label}</span>
                      {option.hint && <span className="text-sm text-ink-soft">{option.hint}</span>}
                    </li>
                  );
                })}
              </ul>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

type FormComboboxProps<T extends FieldValues> = Omit<GroupedComboboxProps, 'value' | 'onChange'> & {
  control: Control<T>;
  name: FieldPath<T>;
};

/** A GroupedCombobox bound to a react-hook-form field. Written as its own component so FormField can hand it the id its label points at. */
export function FormCombobox<T extends FieldValues>({ control, name, ...rest }: FormComboboxProps<T>) {
  return <Controller control={control} name={name} render={({ field }) => <GroupedCombobox {...rest} value={String(field.value ?? '')} onChange={field.onChange} />} />;
}
