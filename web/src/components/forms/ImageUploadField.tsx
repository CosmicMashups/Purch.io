import { useId, useRef, useState } from 'react';
import { userMessage } from '../../lib/apiError';
import { uploadsApi } from '../../features/uploads/api';
import { ACCEPTED_IMAGE_EXTENSIONS, validateImageFile } from '../../features/uploads/imageRules';
import { PurchImage } from '../brand/PurchImage';
import { SecondaryButton } from './FormField';

interface ImageUploadFieldProps {
  label: string;
  value: string | null;
  onChange: (url: string | null) => void;
  /** Bundled pictures the user can pick instead of uploading, e.g. Rice Bowl. The stored value is the assets/ path. */
  samples?: { label: string; value: string }[];
  /** Also lets the user paste the address of a picture that is already hosted somewhere. */
  allowUrl?: boolean;
}

/** Uploads through the API and hands back the hosted URL. Never stores the file itself. */
export function ImageUploadField({ label, value, onChange, samples = [], allowUrl = false }: ImageUploadFieldProps) {
  const id = useId();
  const input = useRef<HTMLInputElement>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [typed, setTyped] = useState<string | null>(null);
  const shown = typed ?? (value && /^https?:\/\//i.test(value) ? value : '');

  function commitUrl() {
    if (typed === null) return;
    const text = typed.trim();
    setTyped(null);
    if (!text) return;
    try {
      const url = new URL(text);
      if (url.protocol !== 'http:' && url.protocol !== 'https:') throw new Error('protocol');
      setError(null);
      onChange(url.toString());
    } catch {
      setError('Enter a full web address that starts with http:// or https://');
    }
  }

  async function onPick(event: React.ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    event.target.value = '';
    if (!file) return;

    const problem = validateImageFile(file);
    if (problem) {
      setError(problem);
      return;
    }

    setError(null);
    setBusy(true);
    try {
      onChange(await uploadsApi.uploadImage(file));
    } catch (uploadError) {
      setError(userMessage(uploadError));
    } finally {
      setBusy(false);
    }
  }

  return (
    <div>
      <p id={`${id}-label`} className="mb-1 text-base font-semibold">
        {label}
      </p>
      <div className="flex items-center gap-4">
        <div className="grid size-20 shrink-0 place-items-center overflow-hidden rounded-control border border-line bg-canvas text-sm text-ink-soft">
          {value ? <PurchImage src={value} alt="" className="size-full object-cover" errorNode={'Not found'} /> : 'No image'}
        </div>
        <div className="flex flex-wrap gap-2">
          <SecondaryButton type="button" disabled={busy} onClick={() => input.current?.click()} aria-describedby={`${id}-label`}>
            {busy ? 'Uploading...' : value ? 'Change image' : 'Upload image'}
          </SecondaryButton>
          {!busy &&
            samples.map((sample) => (
              <SecondaryButton key={sample.value} type="button" onClick={() => onChange(sample.value)} aria-pressed={value === sample.value}>
                {sample.label}
              </SecondaryButton>
            ))}
          {value && !busy && (
            <SecondaryButton type="button" onClick={() => onChange(null)}>
              Remove
            </SecondaryButton>
          )}
        </div>
      </div>
      {allowUrl && (
        <div className="mt-3">
          <label htmlFor={`${id}-url`} className="mb-1 block text-sm font-semibold">
            {label} URL
          </label>
          <input
            id={`${id}-url`}
            type="url"
            inputMode="url"
            placeholder="https://example.com/photo.jpg"
            value={shown}
            disabled={busy}
            onChange={(e) => setTyped(e.target.value)}
            onBlur={commitUrl}
            onKeyDown={(e) => {
              if (e.key !== 'Enter') return;
              e.preventDefault();
              commitUrl();
            }}
            className="h-11 w-full rounded-control border border-ink-soft/40 bg-surface px-3 text-base"
          />
        </div>
      )}
      <input
        ref={input}
        id={id}
        type="file"
        accept={ACCEPTED_IMAGE_EXTENSIONS.join(',')}
        onChange={(e) => void onPick(e)}
        className="sr-only"
        tabIndex={-1}
        aria-hidden="true"
      />
      {error && (
        <p role="alert" className="mt-1 text-sm font-medium text-danger">
          {error}
        </p>
      )}
    </div>
  );
}
