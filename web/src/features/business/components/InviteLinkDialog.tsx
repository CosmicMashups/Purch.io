import QRCode from 'qrcode';
import { useEffect, useState } from 'react';
import { Modal } from '../../../components/Modal';
import { toast } from '../../../components/feedback/toastStore';
import { formatDateTime } from '../../../lib/dates';
import type { InviteLink } from '../memberApi';

export function enrolUrl(token: string): string {
  return `${window.location.origin}/enrol/${token}`;
}

/** Shown once: the server keeps only a hash of the link, so closing this loses it (a new one is one click away). */
export function InviteLinkDialog({ link, onClose }: { link: InviteLink; onClose: () => void }) {
  const url = enrolUrl(link.token);
  const [qr, setQr] = useState<string | null>(null);
  const reset = link.invite.purpose === 1;

  useEffect(() => {
    let cancelled = false;
    QRCode.toDataURL(url, { margin: 1, width: 240 })
      .then((data) => !cancelled && setQr(data))
      .catch(() => !cancelled && setQr(null));
    return () => {
      cancelled = true;
    };
  }, [url]);

  function copy() {
    void navigator.clipboard?.writeText(url).then(() => toast.success('Link copied'), () => toast.error('Could not copy the link'));
  }

  return (
    <Modal
      open
      title={reset ? 'Password reset link' : 'Invitation link'}
      onClose={onClose}
      footer={
        <button type="button" onClick={onClose} className="h-12 rounded-control bg-brand px-6 text-base font-bold text-on-brand">
          Done
        </button>
      }
    >
      <div className="flex flex-col items-center gap-3 text-center">
        <p className="text-base">
          {reset ? `Show this to ${link.invite.name || link.invite.email}. They open it on their own phone to choose a new password.` : `Show this to ${link.invite.name}. They open it on their own phone to set a password and a PIN.`}
        </p>
        {qr ? <img src={qr} alt={`QR code for ${link.invite.email}`} width={240} height={240} className="rounded-control border border-line" /> : <p className="text-sm text-ink-soft">The QR code could not be drawn. Use the link below.</p>}
        <p className="w-full break-all rounded-control bg-canvas px-3 py-2 text-left font-mono text-sm" data-testid="invite-url">
          {url}
        </p>
        <button type="button" onClick={copy} className="h-12 text-base font-semibold text-brand-strong underline">
          Copy link
        </button>
        <p className="text-sm text-ink-soft">It works once and expires {formatDateTime(link.invite.expiresAt)}. No email is sent, and it is not shown again.</p>
      </div>
    </Modal>
  );
}
