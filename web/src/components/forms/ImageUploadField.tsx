import { useId, useRef, useState } from 'react';
import { userMessage } from '../../lib/apiError';
import { uploadsApi } from '../../features/uploads/api';
import { ACCEPTED_IMAGE_EXTENSIONS, validateImageFile } from '../../features/uploads/imageRules';
import { SecondaryButton } from './FormField';

interface ImageUploadFieldProps {
  label: string;
  value: string | null;
  onChange: (url: string | null) => void;
}

/** Uploads through the API and hands back the hosted URL. Never stores the file itself. */
export function ImageUploadField({ label, value, onChange }: ImageUploadFieldProps) {
  const id = useId();
  const input = useRef<HTMLInputElement>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

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
          {value ? <img src={value} alt="" className="size-full object-cover" /> : 'No image'}
        </div>
        <div className="flex flex-wrap gap-2">
          <SecondaryButton type="button" disabled={busy} onClick={() => input.current?.click()} aria-describedby={`${id}-label`}>
            {busy ? 'Uploading...' : value ? 'Change image' : 'Upload image'}
          </SecondaryButton>
          {value && !busy && (
            <SecondaryButton type="button" onClick={() => onChange(null)}>
              Remove
            </SecondaryButton>
          )}
        </div>
      </div>
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
