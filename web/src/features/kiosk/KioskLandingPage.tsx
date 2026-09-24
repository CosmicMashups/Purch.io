import { useState } from 'react';
import { PurchImage } from '../../components/brand/PurchImage';
import { Wordmark } from '../../components/brand/Brand';
import { BUNDLED } from '../../lib/images';
import { Link } from 'react-router-dom';
import { FullscreenButton } from '../../hardware/fullscreen';
import { useKioskBranding } from './queries';
import { ResetDeviceDialog } from './ResetDeviceDialog';
import { useLongPress } from './useLongPress';

export function KioskLandingPage() {
  const branding = useKioskBranding();
  const [resetting, setResetting] = useState(false);
  const hold = useLongPress(() => setResetting(true));
  const poster = branding.data?.kioskPosterImageUrl;

  return (
    <main className="flex flex-1 flex-col justify-between gap-8 px-8 py-10">
      <p {...hold} className="select-none self-center rounded-full border border-brand/30 bg-brand-tint px-4 py-1.5 text-xs font-bold uppercase tracking-widest text-brand-strong">
        Self-service order kiosk
      </p>

      <div className="flex justify-end">
        <FullscreenButton />
      </div>

      <div className="flex flex-col items-center gap-8 text-center">
        <PurchImage
          src={poster}
          fallback={BUNDLED.kioskPoster}
          alt=""
          loading="eager"
          className="aspect-video w-full rounded-panel object-cover"
          errorNode={
            <div className="grid aspect-video w-full place-items-center rounded-panel bg-brand">
              <Wordmark height={72} onBrand />
            </div>
          }
        />
        <div className="flex flex-col gap-3">
          <h1 className="text-5xl font-extrabold tracking-tight">Welcome!</h1>
          <p className="text-xl text-ink-soft">Tap below to start your order.</p>
        </div>
      </div>

      <Link to="/kiosk/menu" className="grid h-20 place-items-center rounded-control bg-brand text-2xl font-bold text-on-brand active:translate-y-px">
        Start your order
      </Link>

      <ResetDeviceDialog open={resetting} onClose={() => setResetting(false)} />
    </main>
  );
}
