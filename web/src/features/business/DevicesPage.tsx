import { useState } from 'react';
import { useForm, useWatch } from 'react-hook-form';
import { ConfirmModal } from '../../components/ConfirmModal';
import { Modal } from '../../components/Modal';
import { toast } from '../../components/feedback/toastStore';
import { EditorCard } from '../../components/forms/EditorCard';
import { FormField, PrimaryButton, controlClass } from '../../components/forms/FormField';
import { FormLoader } from '../../components/forms/FormLoader';
import { ListCard, QueryList } from '../../components/lists/QueryList';
import { PageHeader } from '../../components/PageHeader';
import { formatDateTime } from '../../lib/dates';
import { useBranches } from '../branches/queries';
import type { Branch } from '../branches/types';
import type { Device } from './deviceApi';
import { devicePinProblem, pinRequiredFor } from './deviceRules';
import { useCreateDevice, useDevices, useResetPairingCode, useResetPairingPin } from './deviceQueries';
import { DeviceType, deviceTypeLabels, labelOf } from './types';

const linkButton = 'h-12 text-base font-semibold text-brand-strong underline';

export function DevicesPage() {
  const devices = useDevices();
  const branches = useBranches();
  const resetCode = useResetPairingCode();
  const [codeFor, setCodeFor] = useState<Device | null>(null);
  const [pinFor, setPinFor] = useState<Device | null>(null);

  function confirmResetCode() {
    if (!codeFor) return;
    const device = codeFor;
    setCodeFor(null);
    resetCode.mutate(device.id, { onSuccess: (updated) => toast.success(`New pairing code: ${updated.pairingCode}`) });
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
                <p className="text-base font-semibold">{device.deviceIdentifier ?? labelOf(deviceTypeLabels, device.deviceType)}</p>
                <p className="text-base">
                  {labelOf(deviceTypeLabels, device.deviceType)}, {branches.data?.find((b) => b.id === device.branchId)?.name ?? 'branch'}
                </p>
                <p className="mt-1 font-mono text-2xl font-bold tracking-widest" aria-label={`Pairing code ${device.pairingCode}`}>
                  {device.pairingCode}
                </p>
                <p className="text-sm text-ink-soft">{device.lastSeenAt ? `Last seen ${formatDateTime(device.lastSeenAt)}` : 'Never seen'}</p>
                <div className="mt-2 flex flex-wrap gap-x-5">
                  <button type="button" className={linkButton} onClick={() => setCodeFor(device)}>
                    New pairing code
                  </button>
                  <button type="button" className={linkButton} onClick={() => setPinFor(device)}>
                    Change PIN
                  </button>
                </div>
              </div>
            </ListCard>
          )}
        />
        <FormLoader failed={branches.isError ? branches : null} ready={!!branches.data}>
          {branches.data && <DeviceForm branches={branches.data} />}
        </FormLoader>
      </div>

      <ConfirmModal
        open={codeFor !== null}
        destructive
        title="Make a new pairing code?"
        description="The old code stops working and this device is signed out. Enter the new code on the device to use it again."
        confirmLabel="Make new code"
        onConfirm={confirmResetCode}
        onCancel={() => setCodeFor(null)}
      />
      {pinFor && <ResetPinDialog device={pinFor} onClose={() => setPinFor(null)} />}
    </div>
  );
}

function DeviceForm({ branches }: { branches: Branch[] }) {
  const create = useCreateDevice();
  const { register, control, handleSubmit, reset, setError, formState: { errors } } = useForm({
    defaultValues: { branchId: branches.length === 1 ? branches[0].id : '', deviceIdentifier: '', deviceType: DeviceType.Register as number, pairingPin: '' },
  });
  const type = Number(useWatch({ control, name: 'deviceType' }));

  const submit = handleSubmit((v) => {
    if (!v.branchId) return setError('branchId', { message: 'Choose a branch' });
    const problem = devicePinProblem(Number(v.deviceType), v.pairingPin);
    if (problem) return setError('pairingPin', { message: problem });
    create.mutate(
      { branchId: v.branchId, deviceIdentifier: v.deviceIdentifier.trim() || null, deviceType: Number(v.deviceType), pairingPin: v.pairingPin.trim() || null },
      { onSuccess: (device) => { toast.success(`Device added. Pairing code: ${device.pairingCode}`); reset({ branchId: v.branchId, deviceIdentifier: '', deviceType: DeviceType.Register, pairingPin: '' }); } },
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
      <FormField label="Name (optional)" hint="For example: Front counter tablet">
        <input {...register('deviceIdentifier')} className={controlClass} />
      </FormField>
      <FormField
        label={pinRequiredFor(type) ? 'Pairing PIN' : 'Pairing PIN (optional)'}
        hint={pinRequiredFor(type) ? 'This device signs in with the code and this PIN' : 'Registers sign in with staff PINs instead'}
        error={errors.pairingPin?.message}
      >
        <input type="password" inputMode="numeric" autoComplete="new-password" {...register('pairingPin')} className={controlClass} />
      </FormField>
    </EditorCard>
  );
}

function ResetPinDialog({ device, onClose }: { device: Device; onClose: () => void }) {
  const reset = useResetPairingPin();
  const [pin, setPin] = useState('');
  const [error, setError] = useState<string | undefined>();

  function submit(event: React.FormEvent) {
    event.preventDefault();
    const problem = devicePinProblem(device.deviceType, pin);
    if (problem) return setError(problem);
    setError(undefined);
    reset.mutate({ id: device.id, newPin: pin.trim() }, { onSuccess: () => { toast.success('PIN changed. The device is signed out.'); onClose(); } });
  }

  return (
    <Modal
      open
      title="Change device PIN"
      onClose={onClose}
      footer={
        <PrimaryButton type="submit" form="reset-pin-form" busy={reset.isPending}>
          {reset.isPending ? 'Saving...' : 'Change PIN'}
        </PrimaryButton>
      }
    >
      <form id="reset-pin-form" onSubmit={submit} noValidate>
        <FormField label="New PIN" hint="The device is signed out and must sign in again with it" error={error}>
          <input type="password" inputMode="numeric" autoComplete="new-password" value={pin} onChange={(e) => setPin(e.target.value)} className={controlClass} />
        </FormField>
      </form>
    </Modal>
  );
}
