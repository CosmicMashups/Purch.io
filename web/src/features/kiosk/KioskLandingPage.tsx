import { HandTap } from '@phosphor-icons/react';
import { useState } from 'react';
import { Link } from 'react-router-dom';
import { Wordmark } from '../../components/brand/Brand';
import { PurchImage } from '../../components/brand/PurchImage';
import { BUNDLED } from '../../lib/images';
import { useKioskBranding } from './queries';
import { StaffMenuDialog } from './StaffMenuDialog';
import { useLongPress } from './useLongPress';

/**
 * The welcome screen: the poster fills the screen edge to edge and the whole screen is one big tap target. The only
 * text is the band along the bottom, in the business's main colour. Staff reset the device by holding the top-left
 * corner, which looks like nothing to a customer.
 */
export function KioskLandingPage() {
  const branding = useKioskBranding();
  const [resetting, setResetting] = useState(false);
  const hold = useLongPress(() => setResetting(true));
  const poster = branding.data?.kioskPosterImageUrl;

  return (
    <main className="relative h-dvh overflow-hidden bg-brand">
      <Link to="/kiosk/menu" className="absolute inset-0 flex flex-col focus-visible:outline-offset-[-6px]">
        <div className="min-h-0 flex-1">
          <PurchImage
            src={poster}
            fallback={BUNDLED.kioskPoster}
            alt=""
            loading="eager"
            className="size-full object-cover"
            errorNode={
              <div className="grid size-full place-items-center bg-brand">
                <Wordmark height={72} onBrand />
              </div>
            }
          />
        </div>
        <div className="flex shrink-0 items-center justify-center gap-4 bg-brand px-8 py-8 text-on-brand landscape:py-6">
          <HandTap size={48} weight="duotone" aria-hidden="true" className="motion-safe:animate-[kiosk-nudge_2.4s_ease-in-out_infinite]" />
          <span className="text-4xl font-extrabold tracking-tight landscape:text-5xl">Tap anywhere to begin</span>
        </div>
      </Link>

      <span {...hold} aria-hidden="true" className="absolute left-0 top-0 size-24 select-none" />
      <StaffMenuDialog open={resetting} onClose={() => setResetting(false)} />
    </main>
  );
}
