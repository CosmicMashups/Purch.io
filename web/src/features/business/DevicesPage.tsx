import { useState } from 'react';
import { useForm, useWatch } from 'react-hook-form';
import { ConfirmModal } from '../../components/ConfirmModal';
import { Modal } from '../../components/Modal';
import { StatusBadge } from '../../components/StatusBadge';
import { toast } from '../../components/feedback/toastStore';
import { FormDialog, FormDialogLoader } from '../../components/forms/FormDialog';
import { FormField, PrimaryButton, controlClass } from '../../components/forms/FormField';
import { ListCard, QueryList } from '../../components/lists/QueryList';
import { PageHeader } from '../../components/PageHeader';
import { formatDateTime } from '../../lib/dates';
import { useBranches } from '../branches/queries';
import type { Branch } from '../branches/types';
import type { Device, PairingCode } from './deviceApi';
import { useCreatePairing, useDevices, useNewPairingCode, useRevokeDevice } from './deviceQueries';
import { DeviceStatus, DeviceType, deviceTypeLabels, labelOf } from './types';
import { RowActionsMenu, type RowAction } from '../../components/RowActionsMenu';
import { DeletedRecordsPanel, StatusFilter, type StatusView } from '../lifecycle/StatusFilter';
import { useLifecycle } from '../lifecycle/useLifecycle';
import { useSession } from '../auth/useSession';


export function DevicesPage() {
  const devices = useDevices();
  const branches = useBranches();
  const newCode = useNewPairingCode();
  const revoke = useRevokeDevice();
  const [codeFor, setCodeFor] = useState<Device | null>(null);
  const [revokeFor, setRevokeFor] = useState<Device | null>(null);
  const [shown, setShown] = useState<PairingCode | null>(null);
  const [adding, setAdding] = useState(false);
  const [view, setView] = useState<StatusView>('active');
  const { run, dialog: lifecycleDialog } = useLifecycle();
  // Only an Admin deletes a device; the server enforces it, so the choice is simply not offered to anyone else.
  const isAdmin = useSession().role === 'Admin';

  function pairAgain(device: Device) {
    setCodeFor(null);
    newCode.mutate(device.id, { onSuccess: setShown });
  }

  function confirmRevoke() {
    if (!revokeFor) return;
    const device = revokeFor;
    setRevokeFor(null);
    revoke.mutate(device.id, { onSuccess: () => toast.success(`${nameOf(device)} was revoked and signed out`) });
  }

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Devices"
        subtitle="Registers, kiosks and displays paired to a branch"
        backTo={{ to: '/business', label: 'Business' }}
        action={
          <PrimaryButton type="button" onClick={() => setAdding(true)}>
            Add device
          </PrimaryButton>
        }
      />
      <StatusFilter value={view} onChange={setView} canSeeDeleted={isAdmin} />
      {view === 'deleted' ? (
        <DeletedRecordsPanel kind="Device" noun="devices" />
      ) : (
        <QueryList
          columns
          query={devices}
          errorTitle="Devices could not be loaded"
          emptyMessage={view === 'active' ? 'No devices yet. Add one, then enter its code on the device.' : 'No inactive devices.'}
          transform={(rows) => rows.filter((d) => (view === 'active' ? d.status !== DeviceStatus.Inactive && d.status !== DeviceStatus.Revoked : d.status === DeviceStatus.Inactive || d.status === DeviceStatus.Revoked))}
          renderRow={(device) => {
            const subject = nameOf(device);
            const actions: RowAction[] = [
              { label: 'New pairing code', onSelect: () => (device.status === DeviceStatus.Pending ? pairAgain(device) : setCodeFor(device)) },
            ];
            if (device.status === DeviceStatus.Active || device.status === DeviceStatus.Pending) {
              actions.push({ label: 'Make inactive', onSelect: () => run({ kind: 'Device', id: device.id, name: subject }, 'deactivate'), separated: true });
            }
            if (device.status === DeviceStatus.Inactive) {
              actions.push({ label: 'Make active', onSelect: () => run({ kind: 'Device', id: device.id, name: subject }, 'reactivate'), separated: true });
            }
            if (device.status !== DeviceStatus.Revoked) {
              actions.push({ label: 'Revoke (lost or stolen)', danger: true, onSelect: () => setRevokeFor(device) });
            }
            if (isAdmin && (device.status === DeviceStatus.Inactive || device.status === DeviceStatus.Revoked)) {
              actions.push({ label: 'Delete', danger: true, onSelect: () => run({ kind: 'Device', id: device.id, name: subject }, 'delete') });
            }
            return (
              <ListCard key={device.id}>
                <div className="min-w-0 flex-1">
                  <div className="flex flex-wrap items-center gap-2">
                    <p className="text-base font-semibold">{subject}</p>
                    <StatusBadge {...statusOf(device)} />
                  </div>
                  <p className="text-base">
                    {labelOf(deviceTypeLabels, device.deviceType)}, {branches.data?.find((b) => b.id === device.branchId)?.name ?? 'branch'}
                  </p>
                  {device.linkedRegisterDeviceId && (
                    <p className="text-sm text-ink-soft">Shows {nameOf(devices.data?.find((d) => d.id === device.linkedRegisterDeviceId))}</p>
                  )}
                  <p className="text-sm text-ink-soft">{device.lastSeenAt ? `Last seen ${formatDateTime(device.lastSeenAt)}` : 'Never seen'}</p>
                </div>
                <RowActionsMenu subject={subject} actions={actions} />
              </ListCard>
            );
          }}
        />
      )}

      {adding && (
        <FormDialogLoader title="Add device" failed={branches.isError ? branches : null} ready={!!branches.data} onClose={() => setAdding(false)}>
          {branches.data && (
            <DeviceDialog
              branches={branches.data}
              registers={(devices.data ?? []).filter((d) => d.deviceType === DeviceType.Register && d.status !== DeviceStatus.Revoked)}
              onCreated={(pairing) => {
                setAdding(false);
                setShown(pairing);
              }}
              onClose={() => setAdding(false)}
            />
          )}
        </FormDialogLoader>
      )}

      <ConfirmModal
        open={codeFor !== null}
        destructive
        title="Make a new pairing code?"
        description="This device is signed out and stops working until the new code is entered on it."
        confirmLabel="Make new code"
        onConfirm={() => codeFor && pairAgain(codeFor)}
        onCancel={() => setCodeFor(null)}
      />
      <ConfirmModal
        open={revokeFor !== null}
        destructive
        title="Revoke this device?"
        description="It is signed out at once and cannot be used again unless you make it a new pairing code."
        confirmLabel="Revoke"
        onConfirm={confirmRevoke}
        onCancel={() => setRevokeFor(null)}
      />
      {lifecycleDialog}
      {shown && <PairingCodeDialog pairing={shown} onClose={() => setShown(null)} />}
    </div>
  );
}

function nameOf(device: Device | undefined): string {
  if (!device) return 'a Register';
  return device.name ?? device.deviceIdentifier ?? labelOf(deviceTypeLabels, device.deviceType);
}

function statusOf(device: Device): { label: string; tone: 'success' | 'warning' | 'danger' | 'neutral' } {
  if (device.status === DeviceStatus.Revoked) return { label: 'Revoked', tone: 'danger' };
  if (device.status === DeviceStatus.Inactive) return { label: 'Inactive', tone: 'neutral' };
  if (device.status === DeviceStatus.Pending) return { label: 'Waiting for its code', tone: 'warning' };
  return { label: 'Paired', tone: 'success' };
}

function DeviceDialog({ branches, registers, onCreated, onClose }: { branches: Branch[]; registers: Device[]; onCreated: (pairing: PairingCode) => void; onClose: () => void }) {
  const create = useCreatePairing();
  const { register, control, handleSubmit, setError, formState: { errors } } = useForm({
    defaultValues: { branchId: branches.length === 1 ? branches[0].id : '', name: '', deviceType: DeviceType.Register as number, linkedRegisterDeviceId: '' },
  });
  const type = Number(useWatch({ control, name: 'deviceType' }));
  const branchId = useWatch({ control, name: 'branchId' });
  const sameBranchRegisters = registers.filter((r) => r.branchId === branchId);

  const submit = handleSubmit((v) => {
    if (!v.branchId) return setError('branchId', { message: 'Choose a branch' });
    if (!v.name.trim()) return setError('name', { message: 'Give the device a name' });
    const linksToRegister = Number(v.deviceType) === DeviceType.CustomerDisplay;
    if (linksToRegister && !v.linkedRegisterDeviceId) return setError('linkedRegisterDeviceId', { message: 'Choose the Register this screen shows' });
    create.mutate(
      { branchId: v.branchId, name: v.name.trim(), deviceType: Number(v.deviceType), linkedRegisterDeviceId: linksToRegister ? v.linkedRegisterDeviceId : null },
      {
        onSuccess: onCreated,
      },
    );
  });

  return (
    <FormDialog title="Add device" submitLabel="Add" busy={create.isPending} onSubmit={submit} onClose={onClose}>
      <FormField label="Branch" error={errors.branchId?.message}>
        <select {...register('branchId')} className={controlClass}>
          <option value="">Choose a branch</option>
          {branches.map((b) => (
            <option key={b.id} value={b.id}>
              {b.name}
            </option>
          ))}
        </select>
      </FormField>
      <FormField label="Type">
        <select {...register('deviceType', { valueAsNumber: true })} className={controlClass}>
          {Object.values(DeviceType).map((t) => (
            <option key={t} value={t}>
              {deviceTypeLabels[t]}
            </option>
          ))}
        </select>
      </FormField>
      <FormField label="Name" hint="For example: Front counter till" error={errors.name?.message}>
        <input {...register('name')} className={controlClass} />
      </FormField>
      {type === DeviceType.CustomerDisplay && (
        <FormField label="Shows which Register" hint="The screen follows that Register's order" error={errors.linkedRegisterDeviceId?.message}>
          <select {...register('linkedRegisterDeviceId')} className={controlClass}>
            <option value="">Choose a Register</option>
            {sameBranchRegisters.map((r) => (
              <option key={r.id} value={r.id}>
                {nameOf(r)}
              </option>
            ))}
          </select>
        </FormField>
      )}
    </FormDialog>
  );
}

/** Shown once: the server keeps only a hash, so closing this loses the code (a new one is one click away). */
function PairingCodeDialog({ pairing, onClose }: { pairing: PairingCode; onClose: () => void }) {
  return (
    <Modal open title="Enter this code on the device" onClose={onClose} footer={<button type="button" onClick={onClose} className="h-12 rounded-control bg-brand px-6 text-base font-bold text-on-brand">Done</button>}>
      <div className="flex flex-col gap-3">
        <p className="text-base">
          On {nameOf(pairing.device)}, open this app at <span className="font-mono font-semibold">/pair</span> and type the code below.
        </p>
        <p className="font-mono text-4xl font-bold tracking-widest" aria-label={`Pairing code ${pairing.pairingCode}`}>
          {pairing.pairingCode}
        </p>
        <p className="text-sm text-ink-soft">It works once and expires at {formatDateTime(pairing.expiresAt)}. It is not shown again.</p>
      </div>
    </Modal>
  );
}
