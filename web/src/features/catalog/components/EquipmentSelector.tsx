import { useEffect, useId, useMemo, useRef, useState } from 'react';
import { STATUS_LABEL } from '../../equipment/equipment';
import { EquipmentStatus, type Equipment } from '../../equipment/types';

interface EquipmentSelectorProps {
  /** Everything that can be picked. */
  equipment: Equipment[];
  /** The ids of the equipment this item needs. */
  selected: string[];
  onChange: (selected: string[]) => void;
}

/**
 * Pick the machines an item needs: type to narrow the list, tick the ones you need. While any of them is out of service the
 * item shows as out of stock at the till.
 */
export function EquipmentSelector({ equipment, selected, onChange }: EquipmentSelectorProps) {
  const id = useId();
  const listId = `${id}-list`;
  const root = useRef<HTMLDivElement>(null);
  const [query, setQuery] = useState('');
  const [open, setOpen] = useState(false);
  const [active, setActive] = useState(0);

  const matches = useMemo(() => {
    const needle = query.trim().toLowerCase();
    return needle === '' ? equipment : equipment.filter((e) => e.name.toLowerCase().includes(needle));
  }, [equipment, query]);

  useEffect(() => {
    if (!open) return;
    const close = (event: MouseEvent) => {
      if (!root.current?.contains(event.target as Node)) setOpen(false);
    };
    document.addEventListener('mousedown', close);
    return () => document.removeEventListener('mousedown', close);
  }, [open]);

  function toggle(equipmentId: string) {
    onChange(selected.includes(equipmentId) ? selected.filter((s) => s !== equipmentId) : [...selected, equipmentId]);
  }

  function onKeyDown(event: React.KeyboardEvent<HTMLInputElement>) {
    if (event.key === 'Escape') {
      setOpen(false);
    } else if (event.key === 'ArrowDown') {
      event.preventDefault();
      setOpen(true);
      setActive((a) => Math.min(a + 1, Math.max(matches.length - 1, 0)));
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      setActive((a) => Math.max(a - 1, 0));
    } else if (event.key === 'Enter' && open) {
      // Enter inside a form would submit it; here it ticks the highlighted equipment.
      event.preventDefault();
      const target = matches[active];
      if (target) toggle(target.id);
    }
  }

  const chosen = equipment.filter((e) => selected.includes(e.id));
  const optionId = (index: number) => `${id}-option-${index}`;

  return (
    <div className="flex flex-col gap-3">
      <div ref={root} className="relative">
        <label htmlFor={`${id}-input`} className="mb-1 block text-sm font-medium text-gray-700">
          Required equipment
        </label>
        <input
          id={`${id}-input`}
          role="combobox"
          aria-expanded={open}
          aria-controls={listId}
          aria-autocomplete="list"
          aria-activedescendant={open && matches[active] ? optionId(active) : undefined}
          autoComplete="off"
          placeholder={chosen.length > 0 ? `${chosen.length} selected. Search to add more` : 'Search equipment'}
          value={query}
          onChange={(e) => {
            setQuery(e.target.value);
            setActive(0);
            setOpen(true);
          }}
          onFocus={() => setOpen(true)}
          onKeyDown={onKeyDown}
          className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm"
        />
        {open && (
          <ul id={listId} role="listbox" aria-multiselectable="true" aria-label="Equipment" className="absolute z-20 mt-1 max-h-64 w-full overflow-auto rounded-md border border-gray-200 bg-white py-1 shadow-lg">
            {matches.length === 0 && <li className="px-3 py-2 text-sm text-gray-500">No equipment matches</li>}
            {matches.map((item, index) => {
              const checked = selected.includes(item.id);
              return (
                <li
                  key={item.id}
                  id={optionId(index)}
                  role="option"
                  aria-selected={checked}
                  // mousedown would blur the input before the click lands, closing the list mid-selection.
                  onMouseDown={(e) => e.preventDefault()}
                  onClick={() => toggle(item.id)}
                  onMouseEnter={() => setActive(index)}
                  className={`flex cursor-pointer items-center gap-3 px-3 py-2 text-sm ${index === active ? 'bg-gray-100' : ''}`}
                >
                  <input type="checkbox" checked={checked} readOnly tabIndex={-1} aria-hidden="true" className="pointer-events-none size-4" />
                  <span className="flex-1">{item.name}</span>
                  {item.status !== EquipmentStatus.Operational && <span className="text-xs text-gray-500">{STATUS_LABEL[item.status]}</span>}
                </li>
              );
            })}
          </ul>
        )}
      </div>

      {chosen.length > 0 ? (
        <ul className="flex flex-col gap-2" aria-label="Selected equipment">
          {chosen.map((item) => (
            <li key={item.id} className="flex items-center justify-between gap-3 rounded-md border border-gray-200 bg-white p-3">
              <span className="text-sm font-medium text-gray-900">
                {item.name}
                {item.status === EquipmentStatus.OutOfService && <span className="ml-2 text-xs font-normal text-red-700">Out of service. This item shows as out of stock.</span>}
              </span>
              <button type="button" aria-label={`Remove ${item.name}`} onClick={() => toggle(item.id)} className="text-sm text-gray-500 hover:text-gray-900 hover:underline">
                Remove
              </button>
            </li>
          ))}
        </ul>
      ) : (
        <p className="text-xs text-gray-500">This item shows as out of stock while any equipment you pick here is out of service.</p>
      )}
    </div>
  );
}
