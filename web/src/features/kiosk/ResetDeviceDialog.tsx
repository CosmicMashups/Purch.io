import { useState } from 'react';
import { ConfirmModal } from '../../components/ConfirmModal';
import { signOut } from '../auth/signOut';
import { clearDeviceCredential } from './deviceCredential';

/** Ends this device's session and forgets its credential, so it needs a new one-time code to pair again. Behind a hold or a button. */
export function ResetDeviceDialog({ open, onClose }: { open: boolean; onClose: () => void }) {
  const [busy, setBusy] = useState(false);
  return (
    <ConfirmModal
      open={open}
      title="Reset this device?"
      description="It will stop working until an Admin makes it a new pairing code and someone enters it here."
      confirmLabel="Reset device"
      destructive
      busy={busy}
      onCancel={onClose}
      onConfirm={() => {
        setBusy(true);
        clearDeviceCredential();
        void signOut().finally(() => setBusy(false));
      }}
    />
  );
}
