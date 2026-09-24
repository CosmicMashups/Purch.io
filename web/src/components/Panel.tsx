import { useId, type ReactNode } from 'react';

interface PanelProps {
  title: string;
  subtitle?: string;
  /** A link or button on the right of the heading. */
  action?: ReactNode;
  children: ReactNode;
}

/** A titled surface for content that is not one query, so it shares the look of the dashboard panels. */
export function Panel({ title, subtitle, action, children }: PanelProps) {
  const id = useId();
  return (
    <section aria-labelledby={id} className="rounded-panel border border-line bg-surface p-5">
      <div className="flex flex-wrap items-start justify-between gap-x-4">
        <div>
          <h2 id={id} className="text-lg font-semibold">
            {title}
          </h2>
          {subtitle && <p className="text-sm text-ink-soft">{subtitle}</p>}
        </div>
        {action}
      </div>
      <div className="mt-4">{children}</div>
    </section>
  );
}
