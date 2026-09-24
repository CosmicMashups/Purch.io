import { useId, type ReactElement, cloneElement } from 'react';

export const controlClass =
  'h-12 w-full rounded-control border border-ink-soft/40 bg-surface px-3 text-base text-ink placeholder:text-ink-soft/70 focus:border-brand disabled:opacity-60';

interface FormFieldProps {
  label: string;
  error?: string;
  hint?: string;
  children: ReactElement<{ id?: string; 'aria-invalid'?: boolean; 'aria-describedby'?: string }>;
}

/** Label above, hint and error below, wired to the control for screen readers. Never a placeholder-as-label. */
export function FormField({ label, error, hint, children }: FormFieldProps) {
  const id = useId();
  const describedBy = [hint ? `${id}-hint` : null, error ? `${id}-error` : null].filter(Boolean).join(' ') || undefined;

  return (
    <div>
      <label htmlFor={id} className="mb-1 block text-base font-semibold">
        {label}
      </label>
      {cloneElement(children, { id, 'aria-invalid': !!error, 'aria-describedby': describedBy })}
      {hint && (
        <p id={`${id}-hint`} className="mt-1 text-sm text-ink-soft">
          {hint}
        </p>
      )}
      {error && (
        <p id={`${id}-error`} role="alert" className="mt-1 text-sm font-medium text-danger">
          {error}
        </p>
      )}
    </div>
  );
}

export function PrimaryButton({ busy, children, ...rest }: React.ButtonHTMLAttributes<HTMLButtonElement> & { busy?: boolean }) {
  return (
    <button
      {...rest}
      disabled={busy || rest.disabled}
      className="h-12 rounded-control bg-brand px-6 text-base font-semibold text-on-brand hover:bg-brand-strong active:translate-y-px disabled:opacity-60"
    >
      {children}
    </button>
  );
}

export function SecondaryButton(props: React.ButtonHTMLAttributes<HTMLButtonElement>) {
  return (
    <button
      {...props}
      className="h-12 rounded-control border border-line bg-surface px-6 text-base font-semibold text-ink hover:border-brand disabled:opacity-60"
    />
  );
}
