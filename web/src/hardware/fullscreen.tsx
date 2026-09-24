import { useSyncExternalStore } from 'react';

function subscribe(onChange: () => void): () => void {
  document.addEventListener('fullscreenchange', onChange);
  return () => document.removeEventListener('fullscreenchange', onChange);
}

const isFullscreen = () => Boolean(document.fullscreenElement);

/** A browser cannot lock a device into an app. Full screen only hides the browser's own toolbars. */
export function FullscreenButton({ className = '' }: { className?: string }) {
  const active = useSyncExternalStore(subscribe, isFullscreen, () => false);
  if (!document.fullscreenEnabled) return null;
  return (
    <button
      type="button"
      onClick={() => void (active ? document.exitFullscreen() : document.documentElement.requestFullscreen()).catch(() => undefined)}
      className={`h-12 rounded-control border border-line bg-surface px-4 text-base font-semibold ${className}`}
    >
      {active ? 'Exit full screen' : 'Full screen'}
    </button>
  );
}
