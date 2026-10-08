import { useState } from 'react';
import { SAMPLE_IMAGE } from '../../lib/images';
import { useForm, useWatch } from 'react-hook-form';
import { toast } from '../../components/feedback/toastStore';
import { FormDialog } from '../../components/forms/FormDialog';
import { FormField, PrimaryButton, SecondaryButton, controlClass } from '../../components/forms/FormField';
import { ImageUploadField } from '../../components/forms/ImageUploadField';
import { ListCard, QueryList } from '../../components/lists/QueryList';
import { Modal } from '../../components/Modal';
import { PageHeader } from '../../components/PageHeader';
import { Skeleton } from '../../components/Skeleton';
import { ErrorState } from '../../components/ErrorState';
import { userMessage } from '../../lib/apiError';
import { useSession } from '../auth/useSession';
import { useDepartmentTracking } from '../tenant/queries';
import { useBranchDepartments, useCreateBranch, useCreateDepartment, useUpdateGcash, useUpdateHardware } from '../branches/adminQueries';
import { useBranches } from '../branches/queries';
import type { Branch } from '../branches/types';
import { RowActionsMenu } from '../../components/RowActionsMenu';
import { StatusBadge } from '../../components/StatusBadge';
import { DeletedRecordsPanel, StatusFilter, type StatusView } from '../lifecycle/StatusFilter';
import { useLifecycle } from '../lifecycle/useLifecycle';
import { CashDrawerPolicy, ReceiptPrinterProfile, cashDrawerPolicyLabels, printerProfileLabels } from './types';

export function BranchesPage() {
  const { role } = useSession();
  const isAdmin = role === 'Admin';
  const branches = useBranches();
  const [adding, setAdding] = useState(false);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const selected = branches.data?.find((b) => b.id === selectedId) ?? null;
  const [view, setView] = useState<StatusView>('active');
  const { run, dialog: lifecycleDialog } = useLifecycle();

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Branches"
        subtitle={isAdmin ? undefined : 'Only an admin can add or change branches.'}
        backTo={{ to: '/business', label: 'Business' }}
        action={
          isAdmin ? (
            <PrimaryButton type="button" onClick={() => setAdding(true)}>
              Add branch
            </PrimaryButton>
          ) : undefined
        }
      />
      {isAdmin && <StatusFilter value={view} onChange={setView} />}
      {view === 'deleted' ? (
        <DeletedRecordsPanel kind="Branch" noun="branches" />
      ) : (
        <QueryList
          columns
          query={branches}
          errorTitle="Branches could not be loaded"
          emptyMessage={view === 'active' ? 'No branches yet.' : 'No inactive branches.'}
          transform={(rows) => rows.filter((b) => (view === 'active' ? b.isActive !== false : b.isActive === false))}
          renderRow={(branch) => (
            <ListCard key={branch.id}>
              <div className="min-w-0 flex-1">
                <p className="flex flex-wrap items-center gap-2 text-base font-semibold">
                  {branch.name}
                  {branch.isActive === false && <StatusBadge label="Inactive" tone="warning" />}
                </p>
                {branch.address && <p className="text-base text-ink-soft">{branch.address}</p>}
              </div>
              {isAdmin && (
                <RowActionsMenu
                  subject={branch.name}
                  actions={[
                    { label: 'Settings', onSelect: () => setSelectedId(branch.id) },
                    branch.isActive === false
                      ? { label: 'Make active', onSelect: () => run({ kind: 'Branch', id: branch.id, name: branch.name }, 'reactivate'), separated: true }
                      : { label: 'Make inactive', onSelect: () => run({ kind: 'Branch', id: branch.id, name: branch.name }, 'deactivate'), separated: true },
                    { label: 'Delete', danger: true, onSelect: () => run({ kind: 'Branch', id: branch.id, name: branch.name }, 'delete') },
                  ]}
                />
              )}
            </ListCard>
          )}
        />
      )}
      {lifecycleDialog}
      {isAdmin && adding && <BranchDialog onClose={() => setAdding(false)} />}
      {isAdmin && selected && <BranchDetail key={selected.id} branch={selected} onClose={() => setSelectedId(null)} />}
    </div>
  );
}

function BranchDialog({ onClose }: { onClose: () => void }) {
  const create = useCreateBranch();
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<{ name: string; address: string }>({ defaultValues: { name: '', address: '' } });

  const submit = handleSubmit((v) => {
    if (v.name.trim() === '') return setError('name', { message: 'Enter the branch name' });
    create.mutate({ name: v.name.trim(), address: v.address.trim() || null }, { onSuccess: () => { toast.success('Branch added'); onClose(); } });
  });

  return (
    <FormDialog title="Add branch" submitLabel="Add" busy={create.isPending} onSubmit={submit} onClose={onClose}>
      <FormField label="Name" error={errors.name?.message}>
        <input {...register('name')} className={controlClass} />
      </FormField>
      <FormField label="Address (optional)">
        <input {...register('address')} className={controlClass} />
      </FormField>
    </FormDialog>
  );
}

/** A branch's hardware, GCash QR and departments. Each part saves on its own, so the dialog only closes. */
function BranchDetail({ branch, onClose }: { branch: Branch; onClose: () => void }) {
  return (
    <Modal open wide title={branch.name} onClose={onClose} footer={<div className="flex justify-end"><SecondaryButton type="button" onClick={onClose}>Done</SecondaryButton></div>}>
      <div className="flex flex-col gap-6">
        <HardwareForm branch={branch} />
        <GcashForm branch={branch} />
        {useDepartmentTracking() && <Departments branchId={branch.id} />}
      </div>
    </Modal>
  );
}

function HardwareForm({ branch }: { branch: Branch }) {
  const save = useUpdateHardware();
  const { register, control, handleSubmit } = useForm({
    defaultValues: {
      receiptPrinterProfile: branch.receiptPrinterProfile ?? ReceiptPrinterProfile.None,
      cashDrawerEnabled: branch.cashDrawerEnabled ?? false,
      cashDrawerPolicy: branch.cashDrawerPolicy ?? CashDrawerPolicy.KickOnSaleOnly,
    },
  });
  const drawerOn = useWatch({ control, name: 'cashDrawerEnabled' });

  const submit = handleSubmit((v) =>
    save.mutate(
      { id: branch.id, body: { receiptPrinterProfile: Number(v.receiptPrinterProfile), cashDrawerEnabled: v.cashDrawerEnabled, cashDrawerPolicy: Number(v.cashDrawerPolicy) } },
      { onSuccess: () => toast.success('Hardware settings saved') },
    ),
  );

  return (
    <form onSubmit={submit} className="flex flex-col gap-4 rounded-panel border border-line bg-surface p-5" aria-label="Hardware settings">
      <h3 className="text-lg font-semibold">Receipt printer and cash drawer</h3>
      <p className="text-sm text-ink-soft">These choose what this branch's devices are set up for. Connecting the hardware itself is done at the till.</p>
      <FormField label="Receipt printer">
        <select {...register('receiptPrinterProfile')} className={controlClass}>
          {Object.values(ReceiptPrinterProfile).map((p) => (
            <option key={p} value={p}>
              {printerProfileLabels[p]}
            </option>
          ))}
        </select>
      </FormField>
      <label className="flex h-12 items-center gap-3 text-base font-semibold">
        <input type="checkbox" {...register('cashDrawerEnabled')} className="size-6 accent-brand" />
        This branch has a cash drawer
      </label>
      <FormField label="When the drawer opens">
        <select {...register('cashDrawerPolicy')} disabled={!drawerOn} className={controlClass}>
          {Object.values(CashDrawerPolicy).map((p) => (
            <option key={p} value={p}>
              {cashDrawerPolicyLabels[p]}
            </option>
          ))}
        </select>
      </FormField>
      <div>
        <PrimaryButton type="submit" busy={save.isPending}>
          {save.isPending ? 'Saving...' : 'Save hardware settings'}
        </PrimaryButton>
      </div>
    </form>
  );
}

function GcashForm({ branch }: { branch: Branch }) {
  const save = useUpdateGcash();
  const { register, control, handleSubmit, setValue } = useForm({
    defaultValues: {
      qrImageUrl: branch.manualGcashQrImageUrl ?? '',
      accountName: branch.manualGcashAccountName ?? '',
      accountNumber: branch.manualGcashAccountNumber ?? '',
    },
  });
  const qr = useWatch({ control, name: 'qrImageUrl' });

  const submit = handleSubmit((v) =>
    save.mutate(
      { id: branch.id, body: { qrImageUrl: v.qrImageUrl || null, accountName: v.accountName.trim() || null, accountNumber: v.accountNumber.trim() || null } },
      { onSuccess: () => toast.success('GCash QR saved') },
    ),
  );

  return (
    <form onSubmit={submit} className="flex flex-col gap-4 rounded-panel border border-line bg-surface p-5" aria-label="Manual GCash QR">
      <h3 className="text-lg font-semibold">Manual GCash QR</h3>
      <p className="text-sm text-ink-soft">Cashiers show this QR to customers who pay by GCash. Confirm each payment by hand.</p>
      <input type="hidden" {...register('qrImageUrl')} />
      <ImageUploadField label="QR image" samples={[{ label: 'Use sample', value: SAMPLE_IMAGE.gcashQr }]} value={qr || null} onChange={(url) => setValue('qrImageUrl', url ?? '', { shouldDirty: true })} />
      <FormField label="Account name">
        <input {...register('accountName')} className={controlClass} />
      </FormField>
      <FormField label="Account number">
        <input {...register('accountNumber')} inputMode="numeric" className={controlClass} />
      </FormField>
      <div>
        <PrimaryButton type="submit" busy={save.isPending}>
          {save.isPending ? 'Saving...' : 'Save GCash QR'}
        </PrimaryButton>
      </div>
    </form>
  );
}

function Departments({ branchId }: { branchId: string }) {
  const departments = useBranchDepartments(branchId);
  const create = useCreateDepartment(branchId);
  const { register, handleSubmit, reset, setError, formState: { errors } } = useForm<{ name: string; contact: string }>({ defaultValues: { name: '', contact: '' } });

  const submit = handleSubmit((v) => {
    if (v.name.trim() === '') return setError('name', { message: 'Enter the department name' });
    create.mutate({ name: v.name.trim(), concessionaireContactInfo: v.contact.trim() || null }, { onSuccess: () => { toast.success('Department added'); reset(); } });
  });

  return (
    <section aria-label="Departments" className="flex flex-col gap-4 rounded-panel border border-line bg-surface p-5">
      <h3 className="text-lg font-semibold">Departments</h3>
      {departments.isPending && <Skeleton className="h-16 w-full" />}
      {departments.isError && <ErrorState title="Departments could not be loaded" message={userMessage(departments.error)} onRetry={() => void departments.refetch()} />}
      {departments.isSuccess && departments.data.length === 0 && <p className="text-base text-ink-soft">No departments yet. Add one if concessionaires or sections share this branch.</p>}
      {departments.isSuccess && departments.data.length > 0 && (
        <ul className="flex flex-col gap-2">
          {departments.data.map((d) => (
            <li key={d.id} className="text-base">
              <span className="font-semibold">{d.name}</span>
              {d.concessionaireContactInfo && <span className="text-ink-soft">, {d.concessionaireContactInfo}</span>}
            </li>
          ))}
        </ul>
      )}
      <form onSubmit={submit} noValidate className="flex flex-col gap-3 border-t border-line pt-4">
        <FormField label="New department" error={errors.name?.message}>
          <input {...register('name')} className={controlClass} />
        </FormField>
        <FormField label="Concessionaire contact (optional)">
          <input {...register('contact')} className={controlClass} />
        </FormField>
        <div>
          <SecondaryButton type="submit" disabled={create.isPending}>
            {create.isPending ? 'Adding...' : 'Add department'}
          </SecondaryButton>
        </div>
      </form>
    </section>
  );
}
