import type { FieldErrors, UseFormRegister, FieldValues, Path } from 'react-hook-form';
import { FormField, controlClass } from '../../../components/forms/FormField';
import type { Item } from '../../catalog/types';

export function ItemSelect<T extends FieldValues>({
  label,
  name,
  register,
  errors,
  items,
}: {
  label: string;
  name: Path<T>;
  register: UseFormRegister<T>;
  errors: FieldErrors<T>;
  items: Item[];
}) {
  const message = errors[name]?.message;
  return (
    <FormField label={label} error={typeof message === 'string' ? message : undefined}>
      <select {...register(name)} className={controlClass}>
        <option value="">Choose an item</option>
        {items.map((item) => (
          <option key={item.id} value={item.id}>
            {item.name}
          </option>
        ))}
      </select>
    </FormField>
  );
}

export function ScheduleFields<T extends FieldValues>({
  register,
  errors,
  startName,
  endName,
  endLabel = 'Ends',
}: {
  register: UseFormRegister<T>;
  errors: FieldErrors<T>;
  startName?: Path<T>;
  endName: Path<T>;
  endLabel?: string;
}) {
  const endMessage = errors[endName]?.message;
  return (
    <div className="grid gap-4 sm:grid-cols-2">
      {startName && (
        <FormField label="Starts" hint="Leave blank to start right away">
          <input type="datetime-local" {...register(startName)} className={controlClass} />
        </FormField>
      )}
      <FormField label={endLabel} hint="Leave blank for no end" error={typeof endMessage === 'string' ? endMessage : undefined}>
        <input type="datetime-local" {...register(endName)} className={controlClass} />
      </FormField>
    </div>
  );
}

export function ActiveBadge({ active }: { active: boolean }) {
  return (
    <span
      className={`shrink-0 rounded-full px-3 py-1 text-sm font-semibold ${active ? 'bg-brand-tint text-brand-strong' : 'bg-line text-ink-soft'}`}
    >
      {active ? 'Active' : 'Off'}
    </span>
  );
}

