interface EmptyStateProps {
  title: string;
  description?: string;
  action?: React.ReactNode;
}

export function EmptyState({ title, description, action }: EmptyStateProps) {
  return (
    <div className="flex flex-col items-center justify-center gap-2 rounded-lg border border-dashed border-line px-6 py-12 text-center">
      <p className="text-sm font-medium text-ink">{title}</p>
      {description && <p className="text-sm text-ink-soft">{description}</p>}
      {action}
    </div>
  );
}
