import { useState } from 'react';
import { useForm, useWatch } from 'react-hook-form';
import { ConfirmModal } from '../../components/ConfirmModal';
import { Modal } from '../../components/Modal';
import { StatusBadge } from '../../components/StatusBadge';
import { toast } from '../../components/feedback/toastStore';
import { EditorCard } from '../../components/forms/EditorCard';
import { FormField, controlClass } from '../../components/forms/FormField';
import { FormLoader } from '../../components/forms/FormLoader';
import { ListCard, QueryList } from '../../components/lists/QueryList';
import { PageHeader } from '../../components/PageHeader';
import { formatDateTime } from '../../lib/dates';
import { useBranches } from '../branches/queries';
import type { Branch } from '../branches/types';
import type { Device, PairingCode } from './deviceApi';
import { useCreatePairing, useDevices, useNewPairingCode, useRevokeDevice } from './deviceQueries';
import { DeviceStatus, DeviceType, deviceTypeLabels, labelOf } from './types';

const linkButton = 'h-12 text-base font-semibold text-brand-strong underline';
const dangerLink = 'h-12 text-base font-semibold text-danger underline';

export function DevicesPage() {
  const devices = useDevices();
  const branches = useBranches();
  const newCode = useNewPairingCode();
  const revoke = useRevokeDevice();
  const [codeFor, setCodeFor] = useState<Device | null>(null);
  const [revokeFor, setRevokeFor] = useState<Device | null>(null);
  const [shown, setShown] = useState<PairingCode | null>(null);

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
      <PageHeader title="Devices" subtitle="Registers, kiosks and displays paired to a branch" backTo={{ to: '/business', label: 'Business' }} />
      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(0,26rem)]">
        <QueryList
          query={devices}
          errorTitle="Devices could not be loaded"
          emptyMessage="No devices yet. Add one, then enter its code on the device."
          renderRow={(device) => (
            <ListCard key={device.id}>
              <div className="min-w-0">
                <div className="flex flex-wrap items-center gap-2">
                  <p className="text-base font-semibold">{nameOf(device)}</p>
                  <StatusBadge {...statusOf(device)} />
                </div>
                <p className="text-base">
                  {labelOf(deviceTypeLabels, device.deviceType)}, {branches.data?.find((b) => b.id === device.branchId)?.name ?? 'branch'}
                </p>
                {device.linkedRegisterDeviceId && (
                  <p className="text-sm text-ink-soft">Shows {nameOf(devices.data?.find((d) => d.id === device.linkedRegisterDeviceId))}</p>
                )}
                <p className="text-sm text-ink-soft">{device.lastSeenAt ? `Last seen ${formatDateTime(device.lastSeenAt)}` : 'Never seen'}</p>
                <div className="mt-2 flex flex-wrap gap-x-5">
                  <button type="button" className={linkButton} onClick={() => (device.status === DeviceStatus.Pending ? pairAgain(device) : setCodeFor(device))}>
                    New pairing code
                  </button>
                  {device.status !== DeviceStatus.Revoked && (
                    <button type="button" className={dangerLink} onClick={() => setRevokeFor(device)}>
                      Revoke
                    </button>
                  )}
                </div>
              </div>
            </ListCard>
          )}
        />
        <FormLoader failed={branches.isError ? branches : null} ready={!!branches.data}>
          {branches.data && <DeviceForm branches={branches.data} registers={(devices.data ?? []).filter((d) => d.deviceType === DeviceType.Register && d.status !== DeviceStatus.Revoked)} onCreated={setShown} />}
        </FormLoader>
      </div>

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
  if (device.status === DeviceStatus.Pending) return { label: 'Waiting for its code', tone: 'warning' };
  return { label: 'Paired', tone: 'success' };
}

function DeviceForm({ branches, registers, onCreated }: { branches: Branch[]; registers: Device[]; onCreated: (pairing: PairingCode) => void }) {
  const create = useCreatePairing();
  const { register, control, handleSubmit, reset, setError, formState: { errors } } = useForm({
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
        onSuccess: (pairing) => {
          onCreated(pairing);
          reset({ branchId: v.branchId, name: '', deviceType: DeviceType.Register, linkedRegisterDeviceId: '' });
        },
      },
    );
  });

  return (
    <EditorCard title="device" editing={false} busy={create.isPending} onSubmit={submit} onCancel={() => reset()}>
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
    </EditorCard>
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
