import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { CaretLeft } from '@phosphor-icons/react';

interface PageHeaderProps {
  title: string;
  subtitle?: string;
  /** The page one level up. Back always goes there, not to whatever page came before, so a refresh or a bookmark still has a sensible way out. */
  backTo?: { to: string; label: string };
  action?: ReactNode;
}

export function PageHeader({ title, subtitle, backTo, action }: PageHeaderProps) {
  return (
    <header className="flex flex-wrap items-end justify-between gap-x-4 gap-y-2">
      <div className="min-w-0">
        {backTo && (
          <Link to={backTo.to} className="-ml-2 mb-1 inline-flex h-12 items-center gap-1 rounded-control pl-1 pr-3 text-base font-semibold text-brand-strong hover:bg-canvas">
            <CaretLeft size={20} weight="bold" aria-hidden="true" />
            Back to {backTo.label}
          </Link>
        )}
        <h1 className="text-2xl font-bold tracking-tight sm:text-3xl">{title}</h1>
        {subtitle && <p className="mt-1 text-base text-ink-soft">{subtitle}</p>}
      </div>
      {action}
    </header>
  );
}

export function LinkButton({ to, children, primary = false }: { to: string; children: ReactNode; primary?: boolean }) {
  return (
    <Link
      to={to}
      className={`grid h-12 place-items-center rounded-control px-6 text-base font-semibold ${primary ? 'bg-brand text-on-brand hover:bg-brand-strong' : 'border border-line bg-surface hover:border-brand'}`}
    >
      {children}
    </Link>
  );
}
