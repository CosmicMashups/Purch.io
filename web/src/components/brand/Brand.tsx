import { BUNDLED } from '../../lib/images';
import { useBrandLogo } from '../../theme/useBrandLogo';
import { PurchImage } from './PurchImage';

/** The business's logo in a circle, falling back to the Purch.io logo. Same treatment as the Flutter app bar. */
export function BrandMark({ size = 48, className = '' }: { size?: number; className?: string }) {
  const logo = useBrandLogo();
  return (
    <span className={`inline-grid shrink-0 overflow-hidden rounded-full border border-line/60 bg-brand ${className}`} style={{ width: size, height: size }}>
      <PurchImage src={logo} fallback={BUNDLED.logo} alt="Company logo" loading="eager" className="size-full object-cover" errorNode={<span aria-hidden="true" />} />
    </span>
  );
}

/**
 * The Purch.io wordmark. Its lettering is pale, so on a brand coloured surface it is drawn solid white, exactly
 * as the Flutter client tints it.
 */
export function Wordmark({ height = 24, onBrand = false, className = '' }: { height?: number; onBrand?: boolean; className?: string }) {
  return <img src={BUNDLED.wordmark} alt="Purch.io" height={height} style={{ height, filter: onBrand ? 'brightness(0) invert(1)' : undefined }} className={`w-auto ${className}`} />;
}
