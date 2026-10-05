import { useEffect, useId, useMemo, useRef, useState } from 'react';
import type { InventoryItem } from '../../inventory/types';
import type { RecipeEntry, RecipeSelection } from '../recipe';

interface IngredientSelectorProps {
  /** Everything that can be picked. */
  ingredients: InventoryItem[];
  selection: RecipeSelection;
  onChange: (selection: RecipeSelection) => void;
  /** A problem with one chosen ingredient's quantity. */
  lineError?: { id: string; message: string } | null;
  /** What the ingredients belong to, which only changes the wording: an item's recipe, or a modifier option. */
  subject?: 'item' | 'modifier';
}

const COPY = {
  item: {
    used: 'Used up every order',
    quantity: 'Quantity per order',
    taken: 'Taken from stock every time this item is sold.',
    checkOnly: 'Not deducted when sold. The item shows as out of stock only when this reaches 0, so set its count with Count stock, for example at the end of a shift.',
  },
  modifier: {
    used: 'Used up each time it is chosen',
    quantity: 'Quantity each time it is chosen',
    taken: 'Taken from stock every time a customer chooses this option, on top of what the item itself uses.',
    checkOnly: 'Not deducted when sold. The option shows as sold out only when this reaches 0, so set its count with Count stock, for example at the end of a shift.',
  },
} as const;

/**
 * Pick the ingredients an item is made from: type to narrow the list, tick the ones you need, then say for each whether it is
 * used up on every order (with how much, in its own base unit) or only checked for availability.
 */
function setEntry(selection: RecipeSelection, id: string, patch: Partial<RecipeEntry>): RecipeSelection {
  return { ...selection, [id]: { ...selection[id], ...patch } };
}

export function IngredientSelector({ ingredients, selection, onChange, lineError, subject = 'item' }: IngredientSelectorProps) {
  const copy = COPY[subject];
  const id = useId();
  const listId = `${id}-list`;
  const root = useRef<HTMLDivElement>(null);
  const [query, setQuery] = useState('');
  const [open, setOpen] = useState(false);
  const [active, setActive] = useState(0);

  const matches = useMemo(() => {
    const needle = query.trim().toLowerCase();
    return needle === '' ? ingredients : ingredients.filter((i) => i.name.toLowerCase().includes(needle) || (i.sku ?? '').toLowerCase().includes(needle));
  }, [ingredients, query]);

  useEffect(() => {
    if (!open) return;
    const close = (event: MouseEvent) => {
      if (!root.current?.contains(event.target as Node)) setOpen(false);
    };
    document.addEventListener('mousedown', close);
    return () => document.removeEventListener('mousedown', close);
  }, [open]);

  function toggle(ingredientId: string) {
    const next = { ...selection };
    if (ingredientId in next) delete next[ingredientId];
    // New ingredients start as availability-only, so nothing is deducted until someone says how much.
    else next[ingredientId] = { used: false, quantity: '' };
    onChange(next);
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
      // Enter inside a form would submit it; here it ticks the highlighted ingredient.
      event.preventDefault();
      const target = matches[active];
      if (target) toggle(target.id);
    }
  }

  const chosen = ingredients.filter((i) => i.id in selection);
  const optionId = (index: number) => `${id}-option-${index}`;

  return (
    <div className="flex flex-col gap-3">
      <div ref={root} className="relative">
        <label htmlFor={`${id}-input`} className="mb-1 block text-sm font-medium text-gray-700">
          Ingredients
        </label>
        <input
          id={`${id}-input`}
          role="combobox"
          aria-expanded={open}
          aria-controls={listId}
          aria-autocomplete="list"
          aria-activedescendant={open && matches[active] ? optionId(active) : undefined}
          autoComplete="off"
          placeholder={chosen.length > 0 ? `${chosen.length} selected. Search to add more` : 'Search ingredients'}
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
          <ul
            id={listId}
            role="listbox"
            aria-multiselectable="true"
            aria-label="Ingredients"
            className="absolute z-20 mt-1 max-h-64 w-full overflow-auto rounded-md border border-gray-200 bg-white py-1 shadow-lg"
          >
            {matches.length === 0 && <li className="px-3 py-2 text-sm text-gray-500">No ingredient matches</li>}
            {matches.map((ingredient, index) => {
              const checked = ingredient.id in selection;
              return (
                <li
                  key={ingredient.id}
                  id={optionId(index)}
                  role="option"
                  aria-selected={checked}
                  // mousedown would blur the input before the click lands, closing the list mid-selection.
                  onMouseDown={(e) => e.preventDefault()}
                  onClick={() => toggle(ingredient.id)}
                  onMouseEnter={() => setActive(index)}
                  className={`flex cursor-pointer items-center gap-3 px-3 py-2 text-sm ${index === active ? 'bg-gray-100' : ''}`}
                >
                  <input type="checkbox" checked={checked} readOnly tabIndex={-1} aria-hidden="true" className="pointer-events-none size-4" />
                  <span className="flex-1">{ingredient.name}</span>
                  <span className="text-xs text-gray-500">{ingredient.baseUnit}</span>
                </li>
              );
            })}
          </ul>
        )}
      </div>

      {chosen.length > 0 && (
        <ul className="flex flex-col gap-2" aria-label="Selected ingredients">
          {chosen.map((ingredient) => (
            <li key={ingredient.id} className="rounded-md border border-gray-200 bg-white p-3">
              <div className="flex items-center justify-between gap-3">
                <span className="text-sm font-medium text-gray-900">{ingredient.name}</span>
                <button
                  type="button"
                  aria-label={`Remove ${ingredient.name}`}
                  onClick={() => toggle(ingredient.id)}
                  className="text-sm text-gray-500 hover:text-gray-900 hover:underline"
                >
                  Remove
                </button>
              </div>
              <fieldset className="mt-2">
                <legend className="text-sm font-medium text-gray-700">How is it used?</legend>
                <label className="mt-1 flex items-center gap-2 text-sm text-gray-700">
                  <input
                    type="radio"
                    name={`${id}-mode-${ingredient.id}`}
                    checked={selection[ingredient.id].used}
                    onChange={() => onChange(setEntry(selection, ingredient.id, { used: true }))}
                  />
                  {copy.used}
                </label>
                <label className="mt-1 flex items-center gap-2 text-sm text-gray-700">
                  <input
                    type="radio"
                    name={`${id}-mode-${ingredient.id}`}
                    checked={!selection[ingredient.id].used}
                    onChange={() => onChange(setEntry(selection, ingredient.id, { used: false }))}
                  />
                  Just check it&apos;s in stock
                </label>
              </fieldset>
              {selection[ingredient.id].used ? (
                <div className="mt-2">
                  <label htmlFor={`${id}-qty-${ingredient.id}`} className="block text-sm text-gray-700">
                    {copy.quantity} ({ingredient.baseUnit})
                  </label>
                  <div className="mt-1 flex items-center gap-2">
                    <input
                      id={`${id}-qty-${ingredient.id}`}
                      inputMode="decimal"
                      value={selection[ingredient.id].quantity}
                      onChange={(e) => onChange(setEntry(selection, ingredient.id, { quantity: e.target.value }))}
                      className="w-32 rounded-md border border-gray-300 px-3 py-2 text-sm"
                    />
                    <span className="text-sm font-medium text-gray-700">{ingredient.baseUnit}</span>
                  </div>
                  <p className="mt-1 text-xs text-gray-500">{copy.taken}</p>
                </div>
              ) : (
                <p className="mt-2 text-xs text-gray-500">{copy.checkOnly}</p>
              )}
              {lineError?.id === ingredient.id && (
                <p role="alert" className="mt-1 text-sm text-red-600">
                  {lineError.message}
                </p>
              )}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
