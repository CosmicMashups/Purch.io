import { useState } from 'react';
import { ConfirmModal } from '../../components/ConfirmModal';
import { signOut } from '../auth/signOut';

/** Ends this device's session. Needs the device PIN again to pair, so it is behind a hold or a button. */
export function ResetDeviceDialog({ open, onClose }: { open: boolean; onClose: () => void }) {
  const [busy, setBusy] = useState(false);
  return (
    <ConfirmModal
      open={open}
      title="Reset this device?"
      description="It will stop working until someone pairs it again with its device code and PIN."
      confirmLabel="Reset device"
      destructive
      busy={busy}
      onCancel={onClose}
      onConfirm={() => {
        setBusy(true);
        void signOut().finally(() => setBusy(false));
      }}
    />
  );
}
