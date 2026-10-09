import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { toast } from '../../../components/feedback/toastStore';
import { FormField, PrimaryButton, SecondaryButton, controlClass } from '../../../components/forms/FormField';
import { Modal } from '../../../components/Modal';
import { SearchBar } from '../../../components/SearchBar';
import { Pill } from '../../../components/lists/QueryList';
import { SortableGroupedTable, type SortableColumn, type SortableGroup } from '../../../components/lists/SortableGroupedTable';
import { ErrorState } from '../../../components/ErrorState';
import { SkeletonList } from '../../../components/Skeleton';
import { PageHeader } from '../../../components/PageHeader';
import { RowActionsMenu, type RowAction } from '../../../components/RowActionsMenu';
import { userMessage } from '../../../lib/apiError';
import { DeletedRecordsPanel, StatusFilter, type StatusView } from '../../lifecycle/StatusFilter';
import { useLifecycle } from '../../lifecycle/useLifecycle';
import { KIND_LABEL, KIND_ORDER, STATUS_LABEL, STATUS_TONE, equipmentSchema, parseQuantity, type EquipmentForm } from '../equipment';
import { useCreateEquipment, useEquipment, useReorderEquipment, useSetEquipmentStatus, useUpdateEquipment } from '../queries';
import { EquipmentKind, EquipmentStatus, type Equipment } from '../types';

type Dialog = { mode: 'create' } | { mode: 'edit'; item: Equipment };

/** The status changes offered on a row: every status except the current one. */
const STATUS_ACTIONS: { status: EquipmentStatus; label: string; danger?: boolean }[] = [
  { status: EquipmentStatus.Operational, label: 'Mark operational' },
  { status: EquipmentStatus.NeedsRepair, label: 'Mark needs repair' },
  { status: EquipmentStatus.OutOfService, label: 'Mark out of service', danger: true },
];

export function EquipmentPage() {
  const equipment = useEquipment();
  const [search, setSearch] = useState('');
  const [view, setView] = useState<StatusView>('active');
  const [dialog, setDialog] = useState<Dialog | null>(null);
  const { run, dialog: lifecycleDialog } = useLifecycle();
  const setStatus = useSetEquipmentStatus();
  const reorder = useReorderEquipment();
  const searching = search.trim() !== '';

  async function changeStatus(item: Equipment, status: EquipmentStatus) {
    try {
      await setStatus.mutateAsync({ id: item.id, status });
      toast.success(
        status === EquipmentStatus.OutOfService && item.usedByItemCount > 0
          ? `${item.name} is out of service. ${item.usedByItemCount} ${item.usedByItemCount === 1 ? 'item shows' : 'items show'} as out of stock.`
          : `${item.name} is now ${STATUS_LABEL[status].toLowerCase()}`,
      );
    } catch (error) {
      toast.error(userMessage(error));
    }
  }

  // One group per kind, in a fixed order. Within a group, the saved order.
  const groups: SortableGroup<Equipment>[] = (() => {
    const q = search.trim().toLowerCase();
    const rows = (equipment.data ?? []).filter(
      (e) => (view === 'active' ? e.isActive : !e.isActive) && (q === '' || e.name.toLowerCase().includes(q) || (e.location?.toLowerCase().includes(q) ?? false)),
    );
    const byOrder = (a: Equipment, b: Equipment) => a.sortOrder - b.sortOrder || a.name.localeCompare(b.name);
    return KIND_ORDER.map((kind) => ({ id: String(kind), title: KIND_LABEL[kind], rows: rows.filter((e) => e.kind === kind).sort(byOrder) })).filter((g) => g.rows.length > 0);
  })();

  const columns: SortableColumn<Equipment>[] = [
    {
      header: 'Name',
      cell: (item) => (
        <div className="min-w-0">
          <p className="font-semibold text-gray-900">{item.name}</p>
          {item.location && <p className="text-xs text-gray-500">{item.location}</p>}
        </div>
      ),
    },
    {
      header: 'Status',
      cell: (item) => (
        <div className="flex flex-wrap gap-1">
          <Pill tone={STATUS_TONE[item.status]}>{STATUS_LABEL[item.status]}</Pill>
          {!item.isActive && <Pill>Inactive</Pill>}
        </div>
      ),
    },
    {
      header: 'Quantity',
      className: 'text-gray-600',
      cell: (item) => (item.quantity === null ? '' : item.quantity),
    },
    {
      header: 'Needed by',
      className: 'text-gray-600',
      cell: (item) => (item.usedByItemCount === 0 ? 'No items' : `${item.usedByItemCount} ${item.usedByItemCount === 1 ? 'item' : 'items'}`),
    },
    {
      header: '',
      className: 'text-right',
      cell: (item) => {
        const subject = { kind: 'Equipment' as const, id: item.id, name: item.name };
        const actions: RowAction[] = [
          { label: 'Edit', onSelect: () => setDialog({ mode: 'edit', item }) },
          ...STATUS_ACTIONS.filter((a) => a.status !== item.status).map((a, index) => ({
            label: a.label,
            danger: a.danger,
            separated: index === 0,
            onSelect: () => void changeStatus(item, a.status),
          })),
          item.isActive
            ? { label: 'Make inactive', onSelect: () => run(subject, 'deactivate'), separated: true }
            : { label: 'Make active', onSelect: () => run(subject, 'reactivate'), separated: true },
          { label: 'Delete', danger: true, onSelect: () => run(subject, 'delete') },
        ];
        return (
          <div className="flex justify-end">
            <RowActionsMenu subject={item.name} actions={actions} />
          </div>
        );
      },
    },
  ];

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Equipment"
        subtitle="Machines, furniture and utensils you keep an eye on"
        backTo={{ to: '/inventory', label: 'Inventory' }}
        action={
          <button type="button" onClick={() => setDialog({ mode: 'create' })} className="h-12 rounded-control bg-brand px-5 text-base font-semibold text-on-brand hover:bg-brand-strong">
            Add Equipment
          </button>
        }
      />
      <StatusFilter value={view} onChange={setView} />
      {view === 'deleted' ? (
        <DeletedRecordsPanel kind="Equipment" noun="equipment" />
      ) : (
        <>
          <div className="flex flex-wrap items-center gap-3">
            <SearchBar value={search} onChange={setSearch} placeholder="Search equipment…" />
          </div>
          {searching && <p className="text-xs text-gray-500">Clear the search to change the order of equipment.</p>}
          <div className="min-w-0">
            {equipment.isPending ? (
              <SkeletonList />
            ) : equipment.isError ? (
              <ErrorState title="Equipment could not be loaded" message={userMessage(equipment.error)} onRetry={() => void equipment.refetch()} />
            ) : groups.length === 0 ? (
              <p className="rounded-panel border border-dashed border-ink-soft/40 p-6 text-base text-ink-soft">
                {searching ? 'No equipment matches your search.' : 'No equipment yet. Add a machine to link the items that need it.'}
              </p>
            ) : (
              <SortableGroupedTable
                groups={groups}
                columns={columns}
                getId={(item) => item.id}
                rowLabel={(item) => item.name}
                disabled={reorder.isPending || searching}
                onReorder={(ids) => reorder.mutate(ids, { onError: () => toast.error('Could not save the new order') })}
              />
            )}
          </div>
        </>
      )}
      {lifecycleDialog}
      {dialog && <EquipmentDialog item={dialog.mode === 'edit' ? dialog.item : null} onDone={() => setDialog(null)} />}
    </div>
  );
}

function EquipmentDialog({ item, onDone }: { item: Equipment | null; onDone: () => void }) {
  const create = useCreateEquipment();
  const update = useUpdateEquipment();
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<EquipmentForm>({
    resolver: zodResolver(equipmentSchema),
    defaultValues: item
      ? {
          name: item.name,
          kind: String(item.kind),
          quantity: item.quantity === null ? '' : String(item.quantity),
          location: item.location ?? '',
          notes: item.notes ?? '',
          isActive: item.isActive,
        }
      : { name: '', kind: String(EquipmentKind.Equipment), quantity: '', location: '', notes: '', isActive: true },
  });

  const submit = handleSubmit(async (v) => {
    const quantity = parseQuantity(v.quantity);
    if (!quantity.ok) {
      setError('quantity', { message: quantity.message });
      return;
    }
    const body = {
      name: v.name,
      kind: Number(v.kind) as EquipmentKind,
      quantity: quantity.value,
      location: v.location.trim() || null,
      notes: v.notes.trim() || null,
    };
    try {
      if (item) await update.mutateAsync({ id: item.id, body: { ...body, isActive: v.isActive } });
      else await create.mutateAsync(body);
    } catch (error) {
      toast.error(userMessage(error));
      return;
    }
    toast.success(item ? 'Equipment updated' : 'Equipment added');
    onDone();
  });

  const busy = create.isPending || update.isPending;

  return (
    <Modal
      open
      title={item ? 'Edit Equipment' : 'Add Equipment'}
      onClose={onDone}
      footer={
        <div className="flex justify-end gap-3">
          <SecondaryButton type="button" onClick={onDone}>
            Cancel
          </SecondaryButton>
          <PrimaryButton type="submit" form="equipment-dialog-form" busy={busy}>
            {busy ? 'Saving...' : item ? 'Save changes' : 'Add'}
          </PrimaryButton>
        </div>
      }
    >
      <form id="equipment-dialog-form" onSubmit={submit} noValidate className="flex flex-col gap-4">
        <FormField label="Name" error={errors.name?.message}>
          <input {...register('name')} className={controlClass} />
        </FormField>
        <FormField label="Type">
          <select {...register('kind')} className={controlClass}>
            {KIND_ORDER.map((kind) => (
              <option key={kind} value={kind}>
                {KIND_LABEL[kind]}
              </option>
            ))}
          </select>
        </FormField>
        <FormField label="Quantity (optional)" hint="Only for things counted in bulk, like spoons or chairs" error={errors.quantity?.message}>
          <input inputMode="numeric" {...register('quantity')} className={controlClass} />
        </FormField>
        <FormField label="Location (optional)">
          <input {...register('location')} className={controlClass} />
        </FormField>
        <FormField label="Notes (optional)">
          <textarea {...register('notes')} rows={3} className={`${controlClass} h-auto py-2`} />
        </FormField>
        {item && (
          <label className="flex h-12 items-center gap-3 text-base font-semibold">
            <input type="checkbox" {...register('isActive')} className="size-6 accent-brand" />
            Active
          </label>
        )}
      </form>
    </Modal>
  );
}
