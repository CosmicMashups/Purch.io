import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Modal } from '../../components/Modal';
import { primaryButton, secondaryButton } from './KioskActionBar';
import { ResetDeviceDialog } from './ResetDeviceDialog';

/** What staff get from holding the kiosk's logo: set up the printer, or reset the device. A customer will not find it by accident. */
export function StaffMenuDialog({ open, onClose }: { open: boolean; onClose: () => void }) {
  const navigate = useNavigate();
  const [resetting, setResetting] = useState(false);

  return (
    <>
      <Modal open={open && !resetting} title="Staff menu" onClose={onClose}>
        <div className="flex flex-col gap-3">
          <button
            type="button"
            className={primaryButton}
            onClick={() => {
              onClose();
              navigate('/kiosk/printer');
            }}
          >
            Printer setup
          </button>
          <button type="button" className={secondaryButton} onClick={() => setResetting(true)}>
            Reset this device
          </button>
        </div>
      </Modal>
      <ResetDeviceDialog
        open={open && resetting}
        onClose={() => {
          setResetting(false);
          onClose();
        }}
      />
    </>
  );
}
