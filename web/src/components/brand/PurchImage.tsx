import { useState, type ReactNode } from 'react';
import { resolveImage } from '../../lib/images';

interface PurchImageProps {
  /** A stored image value: an `assets/...` path, a `/uploads/...` path or a full URL. */
  src: string | null | undefined;
  alt: string;
  className?: string;
  /** A bundled picture to show when there is no value, or when the value will not load. */
  fallback?: string;
  /** Shown when nothing loads at all. Without it, nothing is drawn. */
  errorNode?: ReactNode;
  loading?: 'lazy' | 'eager';
}

/**
 * An image the way the Flutter client's PurchImage treats it: the stored value first, then the bundled fallback,
 * then a plain placeholder, so a broken link never shows a broken-image icon.
 */
export function PurchImage(props: PurchImageProps) {
  return <ResolvedImage key={props.src ?? ''} {...props} />;
}

function ResolvedImage({ src, alt, className, fallback, errorNode = null, loading = 'lazy' }: PurchImageProps) {
  const [failed, setFailed] = useState<string[]>([]);
  const candidates = [resolveImage(src), fallback].filter((c): c is string => Boolean(c));
  const current = candidates.find((c) => !failed.includes(c));
  if (!current) return <>{errorNode}</>;
  return <img src={current} alt={alt} loading={loading} className={className} onError={() => setFailed((f) => [...f, current])} />;
}
