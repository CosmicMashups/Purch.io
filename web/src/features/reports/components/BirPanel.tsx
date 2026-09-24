import { useState } from 'react';
import { ConfirmModal } from '../../../components/ConfirmModal';
import { toast } from '../../../components/feedback/toastStore';
import { PrimaryButton, SecondaryButton } from '../../../components/forms/FormField';
import { useSession } from '../../auth/useSession';
import { DeviceRequired } from '../../pos/components/DeviceRequired';
import { useXReading, useZReading } from '../queries';
import type { BirReading } from '../types';
import { ReadingCard } from './ReadingCard';

/** X and Z readings belong to a paired device, so like selling they need a device sign-in. */
export function BirPanel() {
  const { claims } = useSession();
  const x = useXReading();
  const z = useZReading();
  const [reading, setReading] = useState<BirReading | null>(null);
  const [confirmingZ, setConfirmingZ] = useState(false);

  if (!claims?.deviceId) return <DeviceRequired />;

  const busy = x.isPending || z.isPending;

  function takeZ() {
    setConfirmingZ(false);
    z.mutate(undefined, {
      onSuccess: (result) => {
        setReading(result);
        toast.success('Z-reading taken');
      },
    });
  }

  return (
    <div className="flex max-w-2xl flex-col gap-6">
      <div className="flex flex-wrap gap-3 print:hidden">
        <PrimaryButton type="button" busy={x.isPending} disabled={busy} onClick={() => x.mutate(undefined, { onSuccess: setReading })}>
          {x.isPending ? 'Reading...' : 'Take X-reading'}
        </PrimaryButton>
        <SecondaryButton type="button" disabled={busy} onClick={() => setConfirmingZ(true)}>
          Take Z-reading
        </SecondaryButton>
      </div>
      <p className="text-base text-ink-soft print:hidden">
        An X-reading is a mid-shift snapshot and can be repeated. A Z-reading closes the day for this device and cannot be repeated.
      </p>

      {reading && (
        <>
          <ReadingCard reading={reading} />
          <p className="text-sm text-ink-soft">These readings are a best effort and are not yet checked against BIR accreditation requirements.</p>
          <div className="print:hidden">
            <SecondaryButton type="button" onClick={() => window.print()}>
              Print
            </SecondaryButton>
          </div>
        </>
      )}

      <ConfirmModal
        open={confirmingZ}
        title="Take the Z-reading?"
        description="This closes the day for this device. It advances the reset counter and the grand accumulated sales, and it cannot be undone or repeated."
        confirmLabel="Take Z-reading"
        destructive
        onConfirm={takeZ}
        onCancel={() => setConfirmingZ(false)}
      />
    </div>
  );
}
