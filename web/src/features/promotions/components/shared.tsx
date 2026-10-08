import type { FieldErrors, UseFormRegister, FieldValues, Path } from 'react-hook-form';
import { FormField, controlClass } from '../../../components/forms/FormField';
import type { Item } from '../../catalog/types';
import { RowActionsMenu, type RowAction } from '../../../components/RowActionsMenu';
import type { LifecycleAction } from '../../lifecycle/api';

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


/** The ⋯ menu at the end of a promotion row: Edit (where the promotion can be edited), switch on or off, delete. */
export function PromoRowMenu({
  subject,
  active,
  onEdit,
  onLifecycle,
}: {
  subject: string;
  active: boolean;
  onEdit?: () => void;
  onLifecycle: (action: LifecycleAction) => void;
}) {
  const actions: RowAction[] = [];
  if (onEdit) actions.push({ label: 'Edit', onSelect: onEdit });
  actions.push(
    active
      ? { label: 'Make inactive', onSelect: () => onLifecycle('deactivate'), separated: actions.length > 0 }
      : { label: 'Make active', onSelect: () => onLifecycle('reactivate'), separated: actions.length > 0 },
    { label: 'Delete', danger: true, onSelect: () => onLifecycle('delete') },
  );
  return <RowActionsMenu subject={subject} actions={actions} />;
}
