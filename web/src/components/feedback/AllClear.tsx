import { CheckCircle } from '@phosphor-icons/react';

/** Shown when nothing in scope has gone wrong, so an empty list reads as good news and not as missing data. */
export function AllClear({ children }: { children: string }) {
  return (
    <p className="flex items-center gap-2 text-base text-ink-soft">
      <CheckCircle size={20} weight="fill" className="text-ok" aria-hidden="true" />
      {children}
    </p>
  );
}
