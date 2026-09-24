import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';

interface PageHeaderProps {
  title: string;
  subtitle?: string;
  backTo?: { to: string; label: string };
  action?: ReactNode;
}

export function PageHeader({ title, subtitle, backTo, action }: PageHeaderProps) {
  return (
    <header className="flex flex-wrap items-end justify-between gap-4">
      <div>
        {backTo && (
          <Link to={backTo.to} className="mb-1 inline-flex h-12 items-center text-base font-semibold text-brand-strong underline">
            Back to {backTo.label}
          </Link>
        )}
        <h1 className="text-3xl font-bold tracking-tight">{title}</h1>
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
