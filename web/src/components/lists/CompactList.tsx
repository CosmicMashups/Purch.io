import type { ReactNode } from 'react';

/** A dense, single-column list: one hairline-divided row per record, the same shape as the Items and Ingredients lists. */
export function CompactList({ children, label }: { children: ReactNode; label?: string }) {
  return (
    <ul aria-label={label} className="divide-y divide-line rounded-panel border border-line bg-surface">
      {children}
    </ul>
  );
}

/** One row: optional lead (picture), the name and a line of detail, a figure on the right, and the row's menu. */
export function CompactRow({
  lead,
  title,
  detail,
  aside,
  menu,
}: {
  lead?: ReactNode;
  title: ReactNode;
  detail?: ReactNode;
  aside?: ReactNode;
  menu?: ReactNode;
}) {
  return (
    <li className="flex min-h-14 items-center gap-3 px-3 py-1.5 sm:px-4">
      {lead}
      <div className="min-w-0 flex-1">
        <p className="truncate text-base font-semibold">{title}</p>
        {detail && <div className="text-sm text-ink-soft">{detail}</div>}
      </div>
      {aside && <div className="shrink-0 text-right">{aside}</div>}
      {menu}
    </li>
  );
}
