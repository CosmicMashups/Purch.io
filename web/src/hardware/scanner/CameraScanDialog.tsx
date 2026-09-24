import { useEffect, useRef, useState } from 'react';
import { Modal } from '../../components/Modal';
import { cameraScanSupported, detectorCtor } from './cameraSupport';

const FORMATS = ['ean_13', 'ean_8', 'upc_a', 'upc_e', 'code_128', 'code_39', 'qr_code'];

const SCAN_EVERY_MS = 250;

interface CameraScanDialogProps {
  onDetect: (code: string) => void;
  onClose: () => void;
}

export function CameraScanDialog({ onDetect, onClose }: CameraScanDialogProps) {
  const video = useRef<HTMLVideoElement>(null);
  const [error, setError] = useState<string | null>(null);
  const supported = cameraScanSupported();
  const detected = useRef(onDetect);
  useEffect(() => {
    detected.current = onDetect;
  }, [onDetect]);

  useEffect(() => {
    const Detector = detectorCtor();
    if (!supported || !Detector) return;
    let stream: MediaStream | null = null;
    let timer: number | null = null;
    let cancelled = false;
    const detector = new Detector({ formats: FORMATS });

    navigator.mediaDevices
      .getUserMedia({ video: { facingMode: 'environment' } })
      .then((s) => {
        if (cancelled) return s.getTracks().forEach((t) => t.stop());
        stream = s;
        const el = video.current;
        if (el) {
          el.srcObject = s;
          void Promise.resolve(el.play?.()).catch(() => undefined);
        }
        timer = window.setInterval(() => {
          const source = video.current;
          if (!source) return;
          detector
            .detect(source)
            .then((found) => {
              const code = found[0]?.rawValue;
              if (code && !cancelled) detected.current(code);
            })
            .catch(() => undefined);
        }, SCAN_EVERY_MS);
      })
      .catch(() => setError('The camera could not be opened. Allow camera access for this site and try again.'));

    return () => {
      cancelled = true;
      if (timer !== null) window.clearInterval(timer);
      stream?.getTracks().forEach((t) => t.stop());
    };
  }, [supported]);

  return (
    <Modal open title="Scan with camera" onClose={onClose}>
      {!supported ? (
        <p className="text-base">This browser cannot scan with a camera. Use Chrome or Edge on a secure page, or use a barcode scanner.</p>
      ) : error ? (
        <p role="alert" className="text-base font-medium text-danger">
          {error}
        </p>
      ) : (
        <div className="flex flex-col gap-2">
          <video ref={video} muted playsInline aria-label="Camera view" className="aspect-video w-full rounded-control bg-black object-cover" />
          <p className="text-sm text-ink-soft">Hold the barcode steady in view.</p>
        </div>
      )}
    </Modal>
  );
}
