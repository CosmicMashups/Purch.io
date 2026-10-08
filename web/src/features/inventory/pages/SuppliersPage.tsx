import { useState } from 'react';
import { useFieldArray, useForm, type Control, type UseFormRegister } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { toast } from '../../../components/feedback/toastStore';
import { FormDialog } from '../../../components/forms/FormDialog';
import { FormField, PrimaryButton, SecondaryButton, controlClass } from '../../../components/forms/FormField';
import { ListCard, Pill, QueryList } from '../../../components/lists/QueryList';
import { PageHeader } from '../../../components/PageHeader';
import { emptyContact, emptySupplierForm, supplierSchema, type SupplierForm } from '../purchasing';
import { useCreateSupplier, useSuppliers, useUpdateSupplier } from '../queries';
import { CONTACT_MODES, type Supplier, type SupplierRequest } from '../types';
import { RowActionsMenu } from '../../../components/RowActionsMenu';
import { DeletedRecordsPanel, StatusFilter, type StatusView } from '../../lifecycle/StatusFilter';
import { useLifecycle } from '../../lifecycle/useLifecycle';

const blankToNull = (value: string) => value.trim() || null;

function toRequest(v: SupplierForm): SupplierRequest {
  return {
    name: v.name.trim(),
    specialization: blankToNull(v.specialization),
    address: blankToNull(v.address),
    tin: blankToNull(v.tin),
    remarks: blankToNull(v.remarks),
    contacts: v.contacts
      .map((c) => ({
        contactPerson: c.contactPerson.trim(),
        modes: c.modes,
        numbers: c.numbers.map((n) => n.value.trim()).filter(Boolean),
        emails: c.emails.map((e) => e.value.trim()).filter(Boolean),
      }))
      .filter((c) => c.contactPerson || c.numbers.length > 0 || c.emails.length > 0),
  };
}

function toForm(s: Supplier): SupplierForm {
  const contacts = s.contacts.map((c) => ({
    contactPerson: c.contactPerson,
    modes: c.modes,
    numbers: (c.numbers.length > 0 ? c.numbers : ['']).map((value) => ({ value })),
    emails: (c.emails.length > 0 ? c.emails : ['']).map((value) => ({ value })),
  }));
  return {
    name: s.name,
    specialization: s.specialization ?? '',
    address: s.address ?? '',
    tin: s.tin ?? '',
    // Suppliers saved before contacts were structured carry their old text as a remark, so it is not lost on the first edit.
    remarks: s.remarks ?? (s.contacts.length === 0 && s.contactInfo ? s.contactInfo : ''),
    contacts: contacts.length > 0 ? contacts : [emptyContact()],
  };
}

export function SuppliersPage() {
  const suppliers = useSuppliers();
  // null: closed; 'new': adding; otherwise the supplier being edited.
  const [dialog, setDialog] = useState<'new' | Supplier | null>(null);
  const [view, setView] = useState<StatusView>('active');
  const { run, dialog: lifecycleDialog } = useLifecycle();

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Suppliers"
        backTo={{ to: '/inventory', label: 'Inventory' }}
        action={
          <PrimaryButton type="button" onClick={() => setDialog('new')}>
            Add supplier
          </PrimaryButton>
        }
      />
      <StatusFilter value={view} onChange={setView} />
      {view === 'deleted' ? (
        <DeletedRecordsPanel kind="Supplier" noun="suppliers" />
      ) : (
        <QueryList
          query={suppliers}
          errorTitle="Suppliers could not be loaded"
          emptyMessage={view === 'active' ? 'No suppliers yet. Add the first one to start ordering stock.' : 'No inactive suppliers.'}
          transform={(rows) => rows.filter((s) => (view === 'active' ? s.isActive : !s.isActive))}
          renderRow={(s) => (
            <ListCard key={s.id}>
              <button type="button" onClick={() => setDialog(s)} aria-label={`Edit ${s.name}`} className="min-w-0 flex-1 rounded-control text-left">
                <span className="flex flex-wrap items-center gap-2 text-base font-semibold">
                  {s.name}
                  {!s.isActive && <Pill>Inactive</Pill>}
                </span>
                {s.specialization && <span className="block text-sm text-ink-soft">{s.specialization}</span>}
                {s.address && <span className="block text-sm text-ink-soft">{s.address}</span>}
                {s.contactInfo && <span className="block text-base text-ink-soft">{s.contactInfo}</span>}
              </button>
              <RowActionsMenu
                subject={s.name}
                actions={[
                  { label: 'Edit', onSelect: () => setDialog(s) },
                  s.isActive
                    ? { label: 'Make inactive', onSelect: () => run({ kind: 'Supplier', id: s.id, name: s.name }, 'deactivate'), separated: true }
                    : { label: 'Make active', onSelect: () => run({ kind: 'Supplier', id: s.id, name: s.name }, 'reactivate'), separated: true },
                  { label: 'Delete', danger: true, onSelect: () => run({ kind: 'Supplier', id: s.id, name: s.name }, 'delete') },
                ]}
              />
            </ListCard>
          )}
        />
      )}
      {dialog && <SupplierDialog supplier={dialog === 'new' ? null : dialog} onClose={() => setDialog(null)} />}
      {lifecycleDialog}
    </div>
  );
}

function SupplierDialog({ supplier, onClose }: { supplier: Supplier | null; onClose: () => void }) {
  const create = useCreateSupplier();
  const update = useUpdateSupplier();
  const {
    register,
    control,
    handleSubmit,
    formState: { errors },
  } = useForm<SupplierForm>({ resolver: zodResolver(supplierSchema), defaultValues: supplier ? toForm(supplier) : emptySupplierForm() });
  const { fields, append, remove } = useFieldArray({ control, name: 'contacts' });

  const submit = handleSubmit(async (v) => {
    if (supplier) {
      await update.mutateAsync({ id: supplier.id, body: { ...toRequest(v), isActive: supplier.isActive } });
      toast.success('Supplier updated');
    } else {
      await create.mutateAsync(toRequest(v));
      toast.success('Supplier added');
    }
    onClose();
  });

  return (
    <FormDialog
      title={supplier ? 'Edit supplier' : 'Add supplier'}
      wide
      busy={create.isPending || update.isPending}
      submitLabel={supplier ? 'Save changes' : 'Add'}
      onSubmit={submit}
      onClose={onClose}
    >
      <FormField label="Name" error={errors.name?.message}>
        <input {...register('name')} className={controlClass} />
      </FormField>
      <FormField label="Specialization (optional)" hint="What they mainly supply">
        <input {...register('specialization')} className={controlClass} />
      </FormField>
      <FormField label="Address (optional)">
        <input {...register('address')} className={controlClass} />
      </FormField>
      <FormField label="TIN (optional)">
        <input {...register('tin')} className={controlClass} />
      </FormField>

      <fieldset className="flex flex-col gap-4">
        <legend className="text-base font-semibold">Contacts</legend>
        {fields.map((field, index) => (
          <ContactFields
            key={field.id}
            index={index}
            control={control}
            register={register}
            emailErrors={errors.contacts?.[index]?.emails}
            canRemove={fields.length > 1}
            onRemove={() => remove(index)}
          />
        ))}
        <SecondaryButton type="button" onClick={() => append(emptyContact())}>
          Add another contact
        </SecondaryButton>
      </fieldset>

      <FormField label="Remarks (optional)">
        <textarea {...register('remarks')} rows={3} className={controlClass} />
      </FormField>
    </FormDialog>
  );
}

function ContactFields({
  index,
  control,
  register,
  emailErrors,
  canRemove,
  onRemove,
}: {
  index: number;
  control: Control<SupplierForm>;
  register: UseFormRegister<SupplierForm>;
  emailErrors: unknown;
  canRemove: boolean;
  onRemove: () => void;
}) {
  const numbers = useFieldArray({ control, name: `contacts.${index}.numbers` });
  const emails = useFieldArray({ control, name: `contacts.${index}.emails` });

  return (
    <div className="flex flex-col gap-3 rounded-control border border-line p-3">
      <FormField label="Contact person">
        <input {...register(`contacts.${index}.contactPerson`)} className={controlClass} />
      </FormField>

      <fieldset>
        <legend className="mb-1 text-sm font-medium">Mode of contact</legend>
        <div className="flex flex-wrap gap-x-4 gap-y-1">
          {CONTACT_MODES.map((mode) => (
            <label key={mode} className="flex h-12 items-center gap-2 text-base">
              <input type="checkbox" value={mode} {...register(`contacts.${index}.modes`)} className="size-5" />
              {mode}
            </label>
          ))}
        </div>
      </fieldset>

      <div className="flex flex-col gap-2">
        {numbers.fields.map((field, n) => (
          <div key={field.id} className="flex items-end gap-2">
            <div className="flex-1">
              <FormField label={n === 0 ? 'Contact number' : `Contact number ${n + 1}`}>
                <input inputMode="tel" {...register(`contacts.${index}.numbers.${n}.value`)} className={controlClass} />
              </FormField>
            </div>
            {numbers.fields.length > 1 && (
              <button type="button" onClick={() => numbers.remove(n)} className="h-12 text-base font-semibold text-danger underline">
                Remove
              </button>
            )}
          </div>
        ))}
        <button type="button" onClick={() => numbers.append({ value: '' })} className="h-12 self-start text-base font-semibold text-brand underline">
          Add another number
        </button>
      </div>

      <div className="flex flex-col gap-2">
        {emails.fields.map((field, n) => (
          <div key={field.id} className="flex items-end gap-2">
            <div className="flex-1">
              <FormField label={n === 0 ? 'Email address' : `Email address ${n + 1}`} error={(emailErrors as unknown as { value?: { message?: string } }[] | undefined)?.[n]?.value?.message}>
                <input type="email" {...register(`contacts.${index}.emails.${n}.value`)} className={controlClass} />
              </FormField>
            </div>
            {emails.fields.length > 1 && (
              <button type="button" onClick={() => emails.remove(n)} className="h-12 text-base font-semibold text-danger underline">
                Remove
              </button>
            )}
          </div>
        ))}
        <button type="button" onClick={() => emails.append({ value: '' })} className="h-12 self-start text-base font-semibold text-brand underline">
          Add another email
        </button>
      </div>

      {canRemove && (
        <button type="button" onClick={onRemove} className="h-12 self-start text-base font-semibold text-danger underline">
          Remove contact
        </button>
      )}
    </div>
  );
}
