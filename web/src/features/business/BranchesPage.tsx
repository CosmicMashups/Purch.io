import { useState } from 'react';
import { useForm, useWatch } from 'react-hook-form';
import { toast } from '../../components/feedback/toastStore';
import { EditorCard } from '../../components/forms/EditorCard';
import { FormField, PrimaryButton, SecondaryButton, controlClass } from '../../components/forms/FormField';
import { ImageUploadField } from '../../components/forms/ImageUploadField';
import { ListCard, QueryList } from '../../components/lists/QueryList';
import { PageHeader } from '../../components/PageHeader';
import { Skeleton } from '../../components/Skeleton';
import { ErrorState } from '../../components/ErrorState';
import { userMessage } from '../../lib/apiError';
import { useSession } from '../auth/useSession';
import { useBranchDepartments, useCreateBranch, useCreateDepartment, useUpdateGcash, useUpdateHardware } from '../branches/adminQueries';
import { useBranches } from '../branches/queries';
import type { Branch } from '../branches/types';
import { CashDrawerPolicy, ReceiptPrinterProfile, cashDrawerPolicyLabels, printerProfileLabels } from './types';

export function BranchesPage() {
  const { role } = useSession();
  const isAdmin = role === 'Admin';
  const branches = useBranches();
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const selected = branches.data?.find((b) => b.id === selectedId) ?? null;

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Branches" subtitle={isAdmin ? undefined : 'Only an admin can add or change branches.'} backTo={{ to: '/business', label: 'Business' }} />
      <div className={isAdmin ? 'grid gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(0,30rem)]' : ''}>
        <QueryList
          query={branches}
          errorTitle="Branches could not be loaded"
          emptyMessage="No branches yet."
          renderRow={(branch) => (
            <ListCard key={branch.id}>
              <div className="min-w-0">
                <p className="text-base font-semibold">{branch.name}</p>
                {branch.address && <p className="text-base text-ink-soft">{branch.address}</p>}
                {isAdmin && (
                  <button type="button" onClick={() => setSelectedId(branch.id)} className="mt-2 h-12 text-base font-semibold text-brand-strong underline">
                    Manage
                  </button>
                )}
              </div>
            </ListCard>
          )}
        />
        {isAdmin &&
          (selected ? (
            <BranchDetail key={selected.id} branch={selected} onClose={() => setSelectedId(null)} />
          ) : (
            <NewBranch />
          ))}
      </div>
    </div>
  );
}

function NewBranch() {
  const create = useCreateBranch();
  const {
    register,
    handleSubmit,
    reset,
    setError,
    formState: { errors },
  } = useForm<{ name: string; address: string }>({ defaultValues: { name: '', address: '' } });

  const submit = handleSubmit((v) => {
    if (v.name.trim() === '') return setError('name', { message: 'Enter the branch name' });
    create.mutate({ name: v.name.trim(), address: v.address.trim() || null }, { onSuccess: () => { toast.success('Branch added'); reset(); } });
  });

  return (
    <EditorCard title="branch" editing={false} busy={create.isPending} onSubmit={submit} onCancel={() => reset()}>
      <FormField label="Name" error={errors.name?.message}>
        <input {...register('name')} className={controlClass} />
      </FormField>
      <FormField label="Address (optional)">
        <input {...register('address')} className={controlClass} />
      </FormField>
    </EditorCard>
  );
}

function BranchDetail({ branch, onClose }: { branch: Branch; onClose: () => void }) {
  return (
    <div className="flex flex-col gap-6">
      <div className="flex items-center justify-between gap-3">
        <h2 className="text-xl font-bold">{branch.name}</h2>
        <SecondaryButton type="button" onClick={onClose}>
          Close
        </SecondaryButton>
      </div>
      <HardwareForm branch={branch} />
      <GcashForm branch={branch} />
      <Departments branchId={branch.id} />
    </div>
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
      <ImageUploadField label="QR image" value={qr || null} onChange={(url) => setValue('qrImageUrl', url ?? '', { shouldDirty: true })} />
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
