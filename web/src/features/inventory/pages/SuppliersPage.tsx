import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { toast } from '../../../components/feedback/toastStore';
import { EditorCard } from '../../../components/forms/EditorCard';
import { FormField, controlClass } from '../../../components/forms/FormField';
import { ListCard, Pill, QueryList } from '../../../components/lists/QueryList';
import { PageHeader } from '../../../components/PageHeader';
import { supplierSchema, type SupplierForm } from '../purchasing';
import { useCreateSupplier, useSuppliers } from '../queries';

/** The API can list and create suppliers but has no update or delete, so this page does the same. */
export function SuppliersPage() {
  const suppliers = useSuppliers();
  const create = useCreateSupplier();
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<SupplierForm>({ resolver: zodResolver(supplierSchema), defaultValues: { name: '', contactInfo: '' } });

  const submit = handleSubmit(async (v) => {
    await create.mutateAsync({ name: v.name, contactInfo: v.contactInfo.trim() || null });
    toast.success('Supplier added');
    reset();
  });

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Suppliers" backTo={{ to: '/inventory', label: 'Inventory' }} />
      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(0,26rem)]">
        <QueryList
          query={suppliers}
          errorTitle="Suppliers could not be loaded"
          emptyMessage="No suppliers yet. Add the first one to start ordering stock."
          renderRow={(s) => (
            <ListCard key={s.id}>
              <div className="min-w-0">
                <p className="text-base font-semibold">{s.name}</p>
                {s.contactInfo && <p className="text-base text-ink-soft">{s.contactInfo}</p>}
              </div>
              {!s.isActive && <Pill>Inactive</Pill>}
            </ListCard>
          )}
        />
        <EditorCard title="supplier" editing={false} busy={create.isPending} onSubmit={submit} onCancel={() => reset()}>
          <FormField label="Name" error={errors.name?.message}>
            <input {...register('name')} className={controlClass} />
          </FormField>
          <FormField label="Contact info (optional)" hint="Phone, email or address">
            <input {...register('contactInfo')} className={controlClass} />
          </FormField>
        </EditorCard>
      </div>
    </div>
  );
}
