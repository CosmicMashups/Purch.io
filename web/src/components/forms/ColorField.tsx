import { useId } from 'react';
import { validHex } from '../../theme/branding';
import { controlClass } from './FormField';

interface ColorFieldProps {
  label: string;
  value: string;
  onChange: (value: string) => void;
  error?: string;
  hint?: string;
}

/** A hex text box paired with a colour picker. Blank means "use the default". */
export function ColorField({ label, value, onChange, error, hint }: ColorFieldProps) {
  const id = useId();
  const valid = validHex(value);

  return (
    <div>
      <label htmlFor={id} className="mb-1 block text-base font-semibold">
        {label}
      </label>
      <div className="flex items-center gap-3">
        <input
          type="color"
          aria-label={`${label} picker`}
          value={valid ?? '#000000'}
          onChange={(e) => onChange(e.target.value.toUpperCase())}
          className="size-12 shrink-0 cursor-pointer rounded-control border border-line bg-surface p-1"
        />
        <input
          id={id}
          value={value}
          placeholder="#RRGGBB"
          spellCheck={false}
          autoComplete="off"
          onChange={(e) => onChange(e.target.value.trim())}
          aria-invalid={!!error}
          aria-describedby={error ? `${id}-error` : hint ? `${id}-hint` : undefined}
          className={`${controlClass} font-mono uppercase`}
        />
      </div>
      {hint && !error && (
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
